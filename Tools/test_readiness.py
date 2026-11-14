"""Testes locais das regras do quadro. Não acessam rede nem credenciais."""
from copy import deepcopy
from contextlib import redirect_stdout, redirect_stderr
import io
import json
from pathlib import Path
import unittest
from unittest.mock import patch
from urllib.parse import unquote, urlsplit, parse_qs

import sync_readiness as ready
import project_board


def issue(number, task_id, state='open', labels=(), users=()):
    return {'number': number, 'title': task_id + ' · tarefa de teste', 'state': state,
            'labels': [{'name': name} for name in labels],
            'assignees': [{'login': name} for name in users]}


def fixture():
    tasks = ready.validate_backlog([
        {'id': 'CORE-001', 'depends_on': [], 'priority': 'P0'},
        {'id': 'CORE-002', 'depends_on': ['CORE-001'], 'priority': 'P0'},
        {'id': 'SUR-001', 'depends_on': [], 'priority': 'P1'},
        {'id': 'QA-001', 'depends_on': [], 'priority': 'P0'},
    ])
    state = {'repository': ready.REPO, 'private': ready.EXPECTED_PRIVATE, 'project': None,
             'issues': {task_id: {'number': n} for n, task_id in enumerate(tasks, 1)}}
    client = FakeClient([issue(n, task_id) for n, task_id in enumerate(tasks, 1)])
    return tasks, state, client


class FakeClient:
    """Simula respostas e registra cada escrita, inclusive atribuições e paginação."""
    def __init__(self, issues):
        self.issues = {row['number']: deepcopy(row) for row in issues}
        self.requests = []
        self.prs = {}
        self.events = {}
        self.after_timeline = None
        self.after_assign = None
        self.reject_assignment = False

    @property
    def writes(self):
        return [request for request in self.requests if request[0] != 'GET']

    def request(self, path, body=None, method=None):
        method = method or ('POST' if body is not None else 'GET')
        self.requests.append((method, path, deepcopy(body)))
        route = urlsplit(path)
        prefix = 'repos/' + ready.REPO
        if path == prefix and method == 'GET':
            return {'full_name': ready.REPO, 'private': ready.EXPECTED_PRIVATE}
        if route.path == prefix + '/issues' and method == 'GET':
            query = parse_qs(route.query)
            page = int(query.get('page', ['1'])[0])
            rows = sorted(self.issues.values(), key=lambda item: item['number'])
            return deepcopy(rows[(page - 1) * 100:page * 100])
        parts = route.path.removeprefix(prefix + '/').split('/')
        if parts[0] == 'pulls' and method == 'GET':
            if len(parts) == 1:
                page = int(parse_qs(route.query).get('page', ['1'])[0])
                rows = sorted(self.prs.values(), key=lambda pr: pr['number'])
                # A listagem real tem merged_at, mas não tem o booleano merged.
                return [{k: deepcopy(v) for k, v in pr.items() if k != 'merged'}
                        for pr in rows[(page - 1) * 100:page * 100]]
            return deepcopy(self.prs[int(parts[1])])
        if parts[0] != 'issues':
            raise AssertionError((method, path, body))
        row = self.issues[int(parts[1])]
        if len(parts) == 3 and parts[2] == 'timeline' and method == 'GET':
            page = int(parse_qs(route.query).get('page', ['1'])[0])
            events = deepcopy(self.events.get(row['number'], []))
            if self.after_timeline:
                callback, self.after_timeline = self.after_timeline, None
                callback(self)
            return events[(page - 1) * 100:page * 100]
        if len(parts) == 2:
            if method == 'GET':
                return deepcopy(row)
            if method == 'PATCH':
                row.update(body)
                return deepcopy(row)
        if parts[2] == 'labels':
            if method == 'POST':
                names = ready.label_names(row) | set(body['labels'])
                row['labels'] = [{'name': name} for name in sorted(names)]
                return deepcopy(row['labels'])
            if method == 'DELETE':
                name = unquote(parts[3])
                row['labels'] = [label for label in row['labels'] if label['name'] != name]
                return None
        if parts[2] == 'assignees':
            users = ready.assignee_names(row)
            if method == 'POST':
                if not self.reject_assignment:
                    users.update(user.lower() for user in body['assignees'])
                row['assignees'] = [{'login': user} for user in sorted(users)]
                if self.after_assign:
                    callback, self.after_assign = self.after_assign, None
                    callback(self)
                return deepcopy(row)
            if method == 'DELETE':
                users.difference_update(user.lower() for user in body['assignees'])
                row['assignees'] = [{'login': user} for user in sorted(users)]
                return deepcopy(row)
        raise AssertionError((method, path, body))


def snapshot(tasks, state, client):
    return ready.mapped_issues(tasks, state, ready.fetch_issues(client))


def pr_event(base='develop', repo=ready.REPO, merged=True):
    return {'action': 'closed', 'number': 50, 'repository': {'full_name': repo},
            'pull_request': {'number': 50, 'merged': merged,
                             'base': {'ref': base, 'repo': {'full_name': repo}}}}


def add_pr(client, body, base='develop', repo=ready.REPO, merged=True, number=50,
           merged_at='2026-11-14T10:00:00Z'):
    client.prs[number] = {'number': number, 'body': body, 'merged': merged, 'state': 'closed',
                      'merged_at': merged_at if merged else None,
                      'merge_commit_sha': 'a' * 40 if merged else None,
                      'base': {'ref': base, 'repo': {'full_name': repo}}}


class ReadinessTests(unittest.TestCase):
    def test_closed_without_integration_does_not_unlock_dependency(self):
        tasks, state, client = fixture()
        client.issues[1]['state'] = 'closed'
        result = ready.synchronize(client, tasks, state)
        self.assertEqual(result['tasks']['CORE-001']['status'], 'blocked')
        self.assertEqual(result['tasks']['CORE-002']['unmet'], ['CORE-001'])
        self.assertEqual(client.writes, [])

    def test_only_closed_and_integrated_is_done(self):
        tasks, state, client = fixture()
        client.issues[1] = issue(1, 'CORE-001', 'closed', ['integrated', 'status:doing'])
        result = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(result['tasks']['CORE-001']['status'], 'done')
        self.assertEqual(result['tasks']['CORE-002']['status'], 'ready')
        self.assertEqual(ready.label_names(client.issues[1]), {'integrated'})
        self.assertIn('status:ready', ready.label_names(client.issues[2]))

    def test_open_integrated_is_not_done_and_reopening_removes_old_proof(self):
        tasks, state, client = fixture()
        client.issues[1] = issue(1, 'CORE-001', labels=['integrated'])
        before = ready.synchronize(client, tasks, state)
        self.assertEqual(before['tasks']['CORE-002']['status'], 'blocked')
        self.assertIn('integrated', ready.label_names(client.issues[1]))
        applied = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(applied['staleIntegrationRemoved'], ['CORE-001'])
        self.assertNotIn('integrated', ready.label_names(client.issues[1]))
        client.issues[1]['state'] = 'closed'  # fechamento manual posterior
        after = ready.synchronize(client, tasks, state)
        self.assertEqual(after['tasks']['CORE-002']['status'], 'blocked')

    def test_optional_backlog_review_external_block_and_unassignment(self):
        tasks, state, client = fixture()
        client.issues[1] = issue(1, 'CORE-001', labels=['status:doing'])
        client.issues[4] = issue(4, 'QA-001', labels=['status:review'])
        result = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(result['tasks']['CORE-001']['status'], 'ready')
        self.assertNotIn('status:doing', ready.label_names(client.issues[1]))
        self.assertEqual(result['tasks']['SUR-001']['status'], 'backlog')
        self.assertEqual(result['tasks']['QA-001']['status'], 'review')
        client.issues[4]['labels'].append({'name': 'external-blocker'})
        self.assertEqual(ready.synchronize(client, tasks, state)['tasks']['QA-001']['status'], 'blocked')

    def test_unclaimed_optional_stays_in_backlog_but_cannot_bypass_dependencies(self):
        tasks, state, client = fixture()
        tasks['SUR-001']['depends_on'] = ['CORE-001']
        result = ready.synchronize(client, tasks, state)
        self.assertEqual(result['tasks']['SUR-001']['status'], 'backlog')
        self.assertEqual(result['tasks']['SUR-001']['unmet'], ['CORE-001'])
        with self.assertRaises(ready.ReadinessError):
            ready.claim_task(client, 'SUR-001', 'Pessoa', tasks, state)
        self.assertEqual(client.writes, [])

    def test_claim_refuses_blocked_task_without_writes(self):
        tasks, state, client = fixture()
        with self.assertRaisesRegex(ready.ReadinessError, 'Dependências'):
            ready.claim_task(client, 'CORE-002', 'Pessoa', tasks, state)
        self.assertEqual(client.writes, [])

    def test_claim_assigns_and_marks_doing_after_dependencies(self):
        tasks, state, client = fixture()
        client.issues[1] = issue(1, 'CORE-001', 'closed', ['integrated'])
        claimed = ready.claim_task(client, 'CORE-002', 'Pessoa', tasks, state)
        self.assertEqual(claimed['status'], 'doing')
        self.assertEqual(ready.assignee_names(client.issues[2]), {'pessoa'})
        self.assertIn('status:doing', ready.label_names(client.issues[2]))

    def test_wip_counts_another_open_assignment_even_if_blocked(self):
        tasks, state, client = fixture()
        client.issues[2] = issue(2, 'CORE-002', users=['Pessoa'])
        with self.assertRaisesRegex(ready.ReadinessError, 'WIP'):
            ready.claim_task(client, 'CORE-001', 'pessoa', tasks, state)
        self.assertEqual(client.writes, [])

    def test_claim_does_not_steal_review_or_another_persons_task(self):
        tasks, state, client = fixture()
        client.issues[1] = issue(1, 'CORE-001', users=['OutraPessoa'])
        with self.assertRaisesRegex(ready.ReadinessError, 'outra pessoa'):
            ready.claim_task(client, 'CORE-001', 'Pessoa', tasks, state)
        client.issues[1] = issue(1, 'CORE-001', labels=['status:review'], users=['Pessoa'])
        with self.assertRaisesRegex(ready.ReadinessError, 'revisão'):
            ready.claim_task(client, 'CORE-001', 'Pessoa', tasks, state)
        self.assertEqual(client.writes, [])

    def test_claim_refuses_missing_assignment_and_rolls_back_concurrent_wip(self):
        tasks, state, client = fixture()
        client.reject_assignment = True
        with self.assertRaisesRegex(ready.ReadinessError, 'não confirmou'):
            ready.claim_task(client, 'CORE-001', 'Pessoa', tasks, state)
        self.assertNotIn('status:doing', ready.label_names(client.issues[1]))
        client.reject_assignment = False
        client.after_assign = lambda c: c.issues[4]['assignees'].append({'login': 'Pessoa'})
        with self.assertRaisesRegex(ready.ReadinessError, 'WIP'):
            ready.claim_task(client, 'CORE-001', 'Pessoa', tasks, state)
        self.assertEqual(ready.assignee_names(client.issues[1]), set())
        self.assertEqual(ready.assignee_names(client.issues[4]), {'pessoa'})

    def test_optional_can_be_claimed_explicitly_and_existing_claim_is_idempotent(self):
        tasks, state, client = fixture()
        ready.claim_task(client, 'SUR-001', 'Pessoa', tasks, state)
        writes = len(client.writes)
        ready.claim_task(client, 'SUR-001', 'Pessoa', tasks, state)
        self.assertEqual(len(client.writes), writes)

    def test_all_pages_are_read_and_prs_are_excluded(self):
        rows = [issue(n, f'TST-{n:03}') for n in range(1, 202)]
        rows[99]['pull_request'] = {'url': 'example'}
        client = FakeClient(rows)
        result = ready.fetch_issues(client)
        self.assertEqual(len(result), 200)
        self.assertNotIn(100, result)
        self.assertIn(201, result)
        self.assertEqual(len(client.requests), 3)

    def test_missing_mapping_wrong_title_and_cycle_are_safe(self):
        tasks, state, client = fixture()
        state['issues'].pop('CORE-001')
        result = ready.synchronize(client, tasks, state)
        self.assertEqual(result['tasks']['CORE-001']['status'], 'blocked')
        self.assertEqual(result['tasks']['CORE-002']['status'], 'blocked')
        state['issues']['CORE-001'] = {'number': 2}
        with self.assertRaises(ready.ReadinessError):
            snapshot(tasks, state, client)
        with self.assertRaisesRegex(ready.ReadinessError, 'Ciclo'):
            ready.validate_backlog([{'id': 'A', 'depends_on': ['B']}, {'id': 'B', 'depends_on': ['A']}])

    def test_only_exact_closing_lines_outside_code_are_accepted(self):
        body = 'Closes #1\n- Fixes #2.\nResolves #3\nCloses owner/repo#4\nCloses #5, #6\n'
        body += '```text\nCloses #7\n```\n`Closes #8`\n> Closes #9\n$(touch /tmp/nope)\n'
        self.assertEqual(ready.closing_numbers(body), {1, 2, 3})

    def test_pr_merge_marks_only_mapped_issues_and_closes_in_same_run(self):
        tasks, state, client = fixture()
        add_pr(client, 'Closes #1\nFixes #3\nResolves #999\n$(echo untrusted)')
        result = ready.merged_pr_event(client, 'pull_request_target', pr_event(), tasks, state)
        self.assertEqual(result['integrated'], ['CORE-001', 'SUR-001'])
        self.assertEqual(result['ignoredNumbers'], [999])
        self.assertTrue(ready.delivered(client.issues[1]))
        recalculated = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(recalculated['tasks']['CORE-002']['status'], 'ready')

    def test_wrong_repo_wrong_base_or_unmerged_pr_never_marks_integrated(self):
        for base, repo, merged in [('feature/test', ready.REPO, True),
                                   ('develop', 'other/repo', True), ('main', ready.REPO, False)]:
            tasks, state, client = fixture()
            add_pr(client, 'Closes #1', base=base, repo=repo, merged=merged)
            result = ready.merged_pr_event(client, 'pull_request_target', pr_event(base, repo, merged), tasks, state)
            self.assertEqual(result['integrated'], [])
            self.assertEqual(client.writes, [])
        tasks, state, client = fixture()
        add_pr(client, 'Closes #1', base='feature/not-integrated')
        result = ready.merged_pr_event(client, 'pull_request_target', pr_event(), tasks, state)
        self.assertEqual(result['integrated'], [])
        self.assertEqual(client.writes, [])

    def test_merged_main_is_supported_and_issue_close_event_does_not_prove_integration(self):
        tasks, state, client = fixture()
        add_pr(client, 'Closes #1', base='main')
        ready.merged_pr_event(client, 'pull_request_target', pr_event('main'), tasks, state)
        self.assertTrue(ready.delivered(client.issues[1]))
        before = len(client.writes)
        result = ready.merged_pr_event(client, 'issues', {'action': 'closed'}, tasks, state)
        self.assertEqual(result['integrated'], [])
        self.assertEqual(len(client.writes), before)

    def test_missed_merge_event_recovers_closed_issue_and_unlocks_dependents(self):
        tasks, state, client = fixture()
        client.issues[1] = issue(1, 'CORE-001', 'closed', ['status:blocked', 'role:R1'])
        add_pr(client, 'Closes #1')
        result = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(result['tasks']['CORE-001']['status'], 'done')
        self.assertEqual(result['tasks']['CORE-002']['status'], 'ready')
        self.assertEqual(result['integrationRecovery']['recovered'][0]['pr'], 50)
        self.assertEqual(ready.label_names(client.issues[1]), {'role:R1', 'integrated'})
        self.assertFalse(any(r[0] == 'PATCH' and r[1].endswith('/issues/1') for r in client.writes))
        count = len(client.writes)
        again = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(again['integrationRecovery']['recovered'], [])
        self.assertEqual(len(client.writes), count)

    def test_missed_event_can_close_open_issue_but_closes_before_adding_proof(self):
        tasks, state, client = fixture()
        add_pr(client, 'Closes #1')
        ready.synchronize(client, tasks, state, apply=True)
        self.assertTrue(ready.delivered(client.issues[1]))
        writes = [r for r in client.writes if '/issues/1' in r[1]]
        self.assertEqual(writes[0][0], 'PATCH')
        self.assertEqual(writes[0][2], {'state': 'closed', 'state_reason': 'completed'})
        self.assertEqual(writes[1][2], {'labels': ['integrated']})

    def test_check_previews_recovery_without_changing_github_or_status(self):
        tasks, state, client = fixture()
        add_pr(client, 'Closes #1')
        result = ready.synchronize(client, tasks, state)
        self.assertEqual(result['integrationRecovery']['wouldRecover'][0]['task'], 'CORE-001')
        self.assertEqual(result['integrationRecovery']['recovered'], [])
        self.assertEqual(result['tasks']['CORE-002']['status'], 'blocked')
        self.assertEqual(client.writes, [])

    def test_reopened_issue_is_not_reclosed_by_old_merge_even_if_manually_closed_again(self):
        for current_state in ('open', 'closed'):
            tasks, state, client = fixture()
            add_pr(client, 'Closes #1')
            client.issues[1]['state'] = current_state
            client.events[1] = [{'event': 'reopened', 'created_at': '2026-11-14T11:00:00Z'}]
            result = ready.synchronize(client, tasks, state, apply=True)
            self.assertFalse(ready.delivered(client.issues[1]))
            self.assertEqual(client.issues[1]['state'], current_state)
            self.assertEqual(result['integrationRecovery']['skipped'][0]['reason'], 'reopened-after-merge')
            self.assertEqual(result['tasks']['CORE-002']['status'], 'blocked')
            # Reexecutar o antigo evento também não pode fechar de novo.
            ready.merged_pr_event(client, 'pull_request_target', pr_event(), tasks, state)
            self.assertFalse(ready.delivered(client.issues[1]))

    def test_new_merge_after_reopening_restores_integration(self):
        tasks, state, client = fixture()
        client.events[1] = [{'event': 'reopened', 'created_at': '2026-11-14T11:00:00Z'}]
        add_pr(client, 'Closes #1')
        add_pr(client, 'Closes #1', number=51, merged_at='2026-11-14T12:00:00Z')
        result = ready.synchronize(client, tasks, state, apply=True)
        self.assertTrue(ready.delivered(client.issues[1]))
        self.assertEqual(result['integrationRecovery']['recovered'][0]['pr'], 51)

    def test_history_is_paginated_and_last_page_reopening_is_respected(self):
        tasks, state, client = fixture()
        add_pr(client, 'Closes #1')
        client.events[1] = [{'event': 'commented'}] * 100 + [
            {'event': 'reopened', 'created_at': '2026-11-14T11:00:00Z'}]
        result = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(result['integrationRecovery']['recovered'], [])
        self.assertTrue(any('/timeline?per_page=100&page=2' in r[1] for r in client.requests))
        self.assertEqual(client.issues[1]['state'], 'open')

    def test_pull_list_is_paginated_and_only_authorized_confirmed_merges_are_recovered(self):
        tasks, state, client = fixture()
        for number in range(100, 200):
            add_pr(client, 'Sem fechamento de tarefa.', number=number)
        add_pr(client, 'Closes #1', number=200)
        add_pr(client, 'Closes #2', base='feature/not-integrated', number=201)
        add_pr(client, 'Closes #3', merged=False, number=202)
        add_pr(client, 'Closes #4', repo='other/repo', number=203)
        result = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual([r['task'] for r in result['integrationRecovery']['recovered']], ['CORE-001'])
        self.assertTrue(any('/pulls?state=closed' in r[1] and 'page=2' in r[1] for r in client.requests))
        self.assertEqual(client.issues[2]['state'], 'open')
        self.assertEqual(client.issues[3]['state'], 'open')
        self.assertEqual(client.issues[4]['state'], 'open')

    def test_incomplete_merge_proof_wrong_title_and_malformed_dates_fail_safely(self):
        for invalid_field in ('merged', 'merged_at', 'merge_commit_sha'):
            tasks, state, client = fixture()
            add_pr(client, 'Closes #1')
            client.prs[50][invalid_field] = None
            result = ready.synchronize(client, tasks, state, apply=True)
            self.assertEqual(result['integrationRecovery']['recovered'], [])
            self.assertNotIn('integrated', ready.label_names(client.issues[1]))
        tasks, state, client = fixture()
        add_pr(client, 'Closes #1')
        client.issues[1]['title'] = 'Outro assunto sem ID'
        with self.assertRaises(ready.ReadinessError):
            ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(client.writes, [])
        client.issues[1]['title'] = 'CORE-001 · tarefa'
        client.events[1] = [{'event': 'reopened', 'created_at': 'invalid'}]
        with self.assertRaises(ready.ReadinessError):
            ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(client.writes, [])

    def test_explicit_block_and_cancellation_are_not_auto_completed(self):
        for blocker in ('external', 'cancelled'):
            tasks, state, client = fixture()
            add_pr(client, 'Closes #1')
            if blocker == 'external':
                client.issues[1]['labels'].append({'name': 'external-blocker'})
            else:
                client.issues[1].update(state='closed', state_reason='not_planned')
            result = ready.synchronize(client, tasks, state, apply=True)
            self.assertEqual(result['integrationRecovery']['recovered'], [])
            self.assertEqual(result['tasks']['CORE-002']['status'], 'blocked')

    def test_concurrent_reopening_while_reading_history_prevents_recovery(self):
        tasks, state, client = fixture()
        client.issues[1]['state'] = 'closed'
        add_pr(client, 'Closes #1')
        client.after_timeline = lambda c: c.issues[1].update(state='open', updated_at='2026-11-14T12:00:00Z')
        result = ready.synchronize(client, tasks, state, apply=True)
        self.assertEqual(client.issues[1]['state'], 'open')
        self.assertFalse(ready.delivered(client.issues[1]))
        self.assertEqual(result['integrationRecovery']['skipped'][0]['reason'], 'changed-during-check')

    def test_cli_apply_without_event_recovers_merge_and_default_check_stays_readonly(self):
        tasks, state, client = fixture()
        client.issues[1]['state'] = 'closed'
        add_pr(client, 'Closes #1')
        with patch.object(ready, 'Client', return_value=client), patch.object(ready, 'load_local', return_value=(tasks, state)):
            with patch.dict(ready.os.environ, {}, clear=True), redirect_stdout(io.StringIO()):
                self.assertEqual(ready.main([]), 0)
                self.assertEqual(client.writes, [])
                self.assertEqual(ready.main(['--apply']), 0)
        self.assertTrue(ready.delivered(client.issues[1]))

    def test_project_sync_writes_only_changed_statuses_and_reads_all_pages(self):
        tasks, state, client = fixture()
        state['project'] = {'id': 'PVT_TEST', 'url': 'https://github.com/users/test/projects/1',
                            'statusFieldId': 'STATUS', 'statusOptions': {'done': 'DONE', 'ready': 'READY'}}
        for key, entry in state['issues'].items():
            entry['projectItemId'] = 'ITEM_' + key
        calls = []
        def graphql(query, variables=None):
            calls.append((query, variables))
            if query.startswith('mutation'):
                return {}
            second = variables.get('after') == 'NEXT'
            nodes = ([{'id': 'ITEM_CORE-002', 'fieldValueByName': {'optionId': 'BLOCKED'}}] if second else
                     [{'id': 'ITEM_CORE-001', 'fieldValueByName': {'optionId': 'DONE'}}])
            return {'node': {'items': {'nodes': nodes, 'pageInfo': {
                'hasNextPage': not second, 'endCursor': None if second else 'NEXT'}}}}
        client.graphql = graphql
        with patch.object(project_board, 'project_client', return_value=client):
            result = project_board.sync(client, state, {'CORE-001': 'done', 'CORE-002': 'ready'})
        self.assertEqual(result['updated'], 1)
        self.assertEqual(result['unchanged'], 1)
        self.assertEqual(len(calls), 3)
        self.assertNotIn('ITEM_CORE-001', calls[-1][0])
        self.assertIn('ITEM_CORE-002', calls[-1][0])

    def test_apply_preserves_unmanaged_labels_and_check_never_calls_project_hook(self):
        tasks, state, client = fixture()
        client.issues[1] = issue(1, 'CORE-001', labels=['priority:P0', 'role:R1', 'status:blocked'])
        state['project'] = {'id': 'PVT_TEST'}
        calls = []
        hook = lambda c, s, statuses: calls.append(statuses) or 'Project sincronizado.'
        result = ready.synchronize(client, tasks, state, project_hook=hook)
        self.assertEqual(client.writes, [])
        self.assertEqual(calls, [])
        self.assertIn('Somente consulta', result['project'])
        ready.synchronize(client, tasks, state, apply=True, project_hook=hook)
        self.assertEqual(calls[0]['CORE-001'], 'ready')
        self.assertEqual(ready.label_names(client.issues[1]), {'priority:P0', 'role:R1', 'status:ready'})

    def test_missing_project_hook_is_explicit(self):
        tasks, state, client = fixture()
        state['project'] = {'id': 'PVT_TEST'}
        with patch.object(ready.github_setup, 'sync_project', None, create=True):
            result = ready.synchronize(client, tasks, state, apply=True)
        self.assertIn('ignorada explicitamente', result['project'])

    def test_cli_default_is_readonly_and_bad_claim_returns_nonzero(self):
        tasks, state, client = fixture()
        stdout, stderr = io.StringIO(), io.StringIO()
        with patch.object(ready, 'Client', return_value=client), patch.object(ready, 'load_local', return_value=(tasks, state)):
            with redirect_stdout(stdout), redirect_stderr(stderr):
                self.assertEqual(ready.main([]), 0)
                self.assertEqual(ready.main(['--claim', 'CORE-002', '--user', 'Pessoa']), 2)
        self.assertEqual(client.writes, [])
        self.assertEqual(json.loads(stdout.getvalue())['mode'], 'check')
        self.assertIn('Dependências', stderr.getvalue())

    def test_workflow_executes_only_default_branch_code_and_never_injects_pr_body(self):
        text = (Path(__file__).resolve().parents[1] / '.github/workflows/readiness.yml').read_text(encoding='utf-8')
        self.assertIn('pull_request_target:', text)
        self.assertIn('branches: [main, develop]', text)
        self.assertIn('schedule:', text)
        self.assertIn('timeout-minutes:', text)
        self.assertIn('ref: ${{ github.event.repository.default_branch }}', text)
        self.assertIn('persist-credentials: false', text)
        self.assertIn('issues: write', text)
        self.assertNotIn('pull_request.head', text)
        self.assertNotIn('pull_request.body', text)
        self.assertNotIn('write-all', text)


if __name__ == '__main__':
    unittest.main(verbosity=2)
