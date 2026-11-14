"""Confere dependências e recupera integrações comprovadas por PRs com merge.

Sem argumentos, apenas consulta. --apply atualiza os rótulos de disponibilidade.
--claim ID --user LOGIN assume uma tarefa após verificar dependências e WIP <= 1.
Uma dependência só está entregue quando a issue está fechada E tem ``integrated``.
Credenciais são fornecidas pelo Client compartilhado e nunca são gravadas aqui.
"""
from pathlib import Path
from datetime import datetime
import argparse
import json
import os
import re
import sys
from urllib.parse import quote

import github_setup
from github_setup import Client, REPO, ROOT, EXPECTED_PRIVATE


STATUS_LABELS = {key: 'status:' + key for key in ('blocked', 'ready', 'doing', 'review', 'backlog')}
INTEGRATED = 'integrated'
LOGIN = re.compile(r'^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$')
TASK_TITLE = re.compile(r'^\[?([A-Z][A-Z0-9]*-\d{3})\]?(?:\s|$)')
CLOSING_LINE = re.compile(r'^\s*(?:[-*]\s+)?(?:Closes|Fixes|Resolves)[ \t]+#([1-9]\d*)[.!]?\s*$', re.I)


class ReadinessError(RuntimeError):
    """Uma ação foi recusada por regra de dependência, identidade ou WIP."""


def label_names(issue):
    return {item if isinstance(item, str) else item['name'] for item in issue.get('labels', [])}


def assignee_names(issue):
    return {item['login'].lower() for item in issue.get('assignees', [])}


def delivered(issue):
    return bool(issue and issue.get('state') == 'closed' and INTEGRATED in label_names(issue))


def validate_backlog(tasks):
    by_id = {task['id']: task for task in tasks}
    if len(by_id) != len(tasks):
        raise ReadinessError('O backlog contém IDs duplicados.')
    visited, active = set(), set()

    def visit(task_id):
        if task_id in active:
            raise ReadinessError('Ciclo de dependências no backlog: ' + task_id)
        if task_id in visited:
            return
        if task_id not in by_id:
            raise ReadinessError('Dependência desconhecida: ' + task_id)
        active.add(task_id)
        for dependency in by_id[task_id].get('depends_on', []):
            visit(dependency)
        active.remove(task_id)
        visited.add(task_id)

    for task_id in by_id:
        visit(task_id)
    return by_id


def load_local(root=ROOT):
    tasks = json.loads((root / 'Data/backlog.json').read_text(encoding='utf-8-sig'))
    state = json.loads((root / 'Data/github-map.json').read_text(encoding='utf-8-sig'))
    if state.get('repository') != REPO or state.get('private') is not EXPECTED_PRIVATE:
        raise ReadinessError('O mapa deve apontar para o repositório e a visibilidade autorizados.')
    return validate_backlog(tasks), state


def fetch_issues(client):
    """Consulta todas as páginas e ignora PRs devolvidos pelo endpoint de issues."""
    issues, page = {}, 1
    while True:
        batch = client.request(f'repos/{REPO}/issues?state=all&per_page=100&page={page}')
        if not isinstance(batch, list):
            raise ReadinessError('O GitHub não devolveu uma lista de issues.')
        for issue in batch:
            if 'pull_request' not in issue:
                issues[int(issue['number'])] = issue
        if len(batch) < 100:
            break
        page += 1
    return issues


def mapped_issues(tasks, state, remote):
    mapped, used = {}, {}
    for task_id in tasks:
        entry = state.get('issues', {}).get(task_id)
        if not entry:
            mapped[task_id] = None
            continue
        number = int(entry['number'])
        if number in used:
            raise ReadinessError(f'Issues duplicadas no mapa: {used[number]} e {task_id}.')
        used[number] = task_id
        issue = remote.get(number)
        if issue:
            explicit_id = TASK_TITLE.match(issue.get('title', ''))
            if explicit_id and explicit_id.group(1) != task_id:
                raise ReadinessError(f'O título de #{number} não corresponde a {task_id}.')
        mapped[task_id] = issue
    return mapped


def calculate(tasks, mapped):
    """O DAG local é a fonte de verdade; rótulos de uma dependência não o substituem."""
    result = {}
    for task_id, task in tasks.items():
        issue = mapped.get(task_id)
        unmet = [dep for dep in task.get('depends_on', []) if not delivered(mapped.get(dep))]
        if not issue:
            key, reason = 'blocked', 'Issue ainda não mapeada ou indisponível no GitHub.'
        elif delivered(issue):
            key, reason = 'done', 'Fechada e integrada.'
        elif issue.get('state') == 'closed':
            key, reason = 'blocked', 'Fechada sem prova de integração; não libera dependentes.'
        elif 'external-blocker' in label_names(issue):
            key, reason = 'blocked', 'Bloqueio externo registrado.'
        elif task.get('priority') in ('P1', 'P2') and not assignee_names(issue) and 'status:review' not in label_names(issue):
            key, reason = 'backlog', 'Opcional não iniciado; bloqueadores continuam listados e são exigidos ao assumir.'
        elif unmet:
            key, reason = 'blocked', 'Aguardando integração de: ' + ', '.join(unmet)
        elif 'status:review' in label_names(issue):
            key, reason = 'review', 'Entrega em revisão.'
        elif assignee_names(issue):
            key, reason = 'doing', 'Tarefa assumida.'
        elif task.get('priority') in ('P1', 'P2'):
            key, reason = 'backlog', 'Opcional disponível; depende de decisão de escopo.'
        else:
            key, reason = 'ready', 'Dependências integradas; pode ser assumida.'
        result[task_id] = {'status': key, 'reason': reason, 'unmet': unmet,
                           'number': issue.get('number') if issue else None}
    return result


def set_status(client, issue, status):
    """Muda apenas os rótulos geridos aqui, preservando capítulo, papel e prioridade."""
    current = label_names(issue)
    desired = STATUS_LABELS.get(status)
    changes = []
    for label in STATUS_LABELS.values():
        if label in current and label != desired:
            client.request(f"repos/{REPO}/issues/{issue['number']}/labels/{quote(label, safe='')}", method='DELETE')
            changes.append('remove ' + label)
    if desired and desired not in current:
        client.request(f"repos/{REPO}/issues/{issue['number']}/labels", {'labels': [desired]}, 'POST')
        changes.append('add ' + desired)
    return changes


def remove_stale_integration(client, mapped):
    """Reabrir invalida a prova antiga; fechar de novo à mão não pode reutilizá-la."""
    removed = []
    for task_id, issue in mapped.items():
        if issue and issue.get('state') != 'closed' and INTEGRATED in label_names(issue):
            client.request(f"repos/{REPO}/issues/{issue['number']}/labels/{INTEGRATED}", method='DELETE')
            issue['labels'] = [label for label in issue.get('labels', [])
                               if (label if isinstance(label, str) else label['name']) != INTEGRATED]
            removed.append(task_id)
    return removed


def closing_numbers(body):
    """Uma diretiva por linha. Blocos e spans de código não fecham tarefas."""
    numbers, fence = set(), None
    for line in (body or '').splitlines():
        stripped = line.lstrip()
        if stripped.startswith(('```', '~~~')):
            mark = stripped[:3]
            fence = None if fence == mark else (mark if fence is None else fence)
            continue
        if fence is not None or '`' in line:
            continue
        match = CLOSING_LINE.fullmatch(line)
        if match:
            numbers.add(int(match.group(1)))
    return numbers


def fetch_pages(client, path):
    rows, page = [], 1
    separator = '&' if '?' in path else '?'
    while True:
        batch = client.request(f'{path}{separator}per_page=100&page={page}')
        if not isinstance(batch, list):
            raise ReadinessError('O GitHub não devolveu uma lista: ' + path)
        rows.extend(batch)
        if len(batch) < 100:
            return rows
        page += 1


def timestamp(value):
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
        if parsed.tzinfo is None:
            raise ValueError('Fuso ausente.')
        return parsed
    except (AttributeError, TypeError, ValueError):
        raise ReadinessError('Data ausente ou inválida na prova de integração.') from None


def authorized_merge(pr):
    base = pr.get('base', {})
    return (pr.get('state') == 'closed' and pr.get('merged') is True
            and bool(pr.get('merged_at')) and bool(pr.get('merge_commit_sha'))
            and base.get('repo', {}).get('full_name') == REPO
            and base.get('ref') in ('main', 'develop'))


def integrate_issue(client, task_id, issue_number, pr, apply):
    """Uma reabertura posterior ao merge invalida sua prova, inclusive em reexecuções."""
    path = f'repos/{REPO}/issues/{issue_number}'
    issue = client.request(path)
    match = TASK_TITLE.match(issue.get('title', ''))
    if 'pull_request' in issue or not match or match.group(1) != task_id:
        raise ReadinessError(f'O mapa não corresponde à issue #{issue_number}.')
    if delivered(issue):
        return 'already-integrated'
    if 'external-blocker' in label_names(issue) or issue.get('state_reason') == 'not_planned':
        return 'explicit-blocker-or-cancelled'
    events = fetch_pages(client, path + '/timeline')
    merged_at = timestamp(pr['merged_at'])
    if any(timestamp(event.get('created_at')) >= merged_at
           for event in events if event.get('event') == 'reopened'):
        return 'reopened-after-merge'
    # Outra pessoa pode ter alterado a tarefa enquanto o histórico era consultado.
    fresh = client.request(path)
    if any(fresh.get(key) != issue.get(key) for key in ('state', 'state_reason', 'updated_at', 'labels')):
        return 'changed-during-check'
    if not apply:
        return 'would-integrate'
    # Fechar primeiro evita expor uma issue aberta com uma prova recém-adicionada.
    if fresh.get('state') != 'closed':
        client.request(path, {'state': 'closed', 'state_reason': 'completed'}, 'PATCH')
    fresh = client.request(path)
    if fresh.get('state') != 'closed':
        return 'changed-during-check'
    # Reconsultar o histórico também protege de uma reabertura durante o fechamento.
    if any(timestamp(event.get('created_at')) >= merged_at
           for event in fetch_pages(client, path + '/timeline') if event.get('event') == 'reopened'):
        return 'reopened-after-merge'
    if INTEGRATED not in label_names(fresh):
        client.request(path + '/labels', {'labels': [INTEGRATED]}, 'POST')
    return 'integrated'


def recover_integrations(client, tasks, state, mapped, apply=False):
    """Reconcilia o histórico; não depende da entrega de um webhook específico."""
    pending = {int(state['issues'][task_id]['number']): task_id
               for task_id, issue in mapped.items() if issue and not delivered(issue)}
    result = {'recovered': [], 'wouldRecover': [], 'skipped': []}
    if not pending:
        return result
    prs = fetch_pages(client, f'repos/{REPO}/pulls?state=closed&sort=updated&direction=desc')
    # O endpoint de listagem não contém o booleano merged: confirmar cada candidato.
    candidates = [pr for pr in prs if pr.get('merged_at')
                  and closing_numbers(pr.get('body')) & pending.keys()]
    candidates.sort(key=lambda pr: timestamp(pr['merged_at']), reverse=True)
    for listed in candidates:
        requested = closing_numbers(listed.get('body')) & pending.keys()
        if not requested:
            continue
        pr = client.request(f"repos/{REPO}/pulls/{listed['number']}")
        if not authorized_merge(pr):
            continue
        for number in sorted(closing_numbers(pr.get('body')) & requested):
            task_id = pending[number]
            outcome = integrate_issue(client, task_id, number, pr, apply)
            evidence = {'task': task_id, 'issue': number, 'pr': pr['number'],
                        'mergedAt': pr['merged_at'], 'mergeCommit': pr['merge_commit_sha']}
            if outcome in ('integrated', 'would-integrate'):
                result['recovered' if apply else 'wouldRecover'].append(evidence)
            elif outcome != 'already-integrated':
                result['skipped'].append({**evidence, 'reason': outcome})
            # Um merge mais antigo também não pode corrigir uma reabertura posterior.
            pending.pop(number)
    return result


def merged_pr_event(client, event_name, event, tasks, state):
    """Usa somente dados de um PR efetivamente integrado no repositório autorizado."""
    ignored = {'integrated': [], 'ignoredNumbers': [], 'reason': 'Evento não integra tarefas.'}
    if event_name not in ('pull_request_target', 'pull_request') or event.get('action') != 'closed':
        return ignored
    payload_pr = event.get('pull_request', {})
    if event.get('repository', {}).get('full_name') != REPO:
        return {**ignored, 'reason': 'Repositório do evento não autorizado.'}
    if not payload_pr.get('merged'):
        return {**ignored, 'reason': 'PR fechado sem merge.'}
    base = payload_pr.get('base', {})
    if base.get('repo', {}).get('full_name') != REPO or base.get('ref') not in ('main', 'develop'):
        return {**ignored, 'reason': 'Base do PR não é main/develop deste repositório.'}
    number = payload_pr.get('number') or event.get('number')
    if not isinstance(number, int) or number <= 0:
        raise ReadinessError('Número de PR inválido no evento.')
    # Confirmar o estado pela API evita confiar somente no JSON do evento.
    pr = client.request(f'repos/{REPO}/pulls/{number}')
    if not authorized_merge(pr):
        return {**ignored, 'reason': 'A API não confirmou merge na base autorizada.'}
    allowed = {int(entry['number']): task_id for task_id, entry in state.get('issues', {}).items()
               if task_id in tasks}
    requested = closing_numbers(pr.get('body', ''))
    integrated = []
    for issue_number in sorted(requested & allowed.keys()):
        task_id = allowed[issue_number]
        outcome = integrate_issue(client, task_id, issue_number, pr, apply=True)
        if outcome in ('integrated', 'already-integrated'):
            integrated.append(task_id)
    return {'integrated': integrated, 'ignoredNumbers': sorted(requested - allowed.keys()),
            'reason': 'Somente diretivas de fechamento de tarefas mapeadas foram processadas.'}


def validate_claim(task_id, user, tasks, mapped):
    if not LOGIN.fullmatch(user or '') or '--' in user:
        raise ReadinessError('Informe um login GitHub válido em --user.')
    if task_id not in tasks or not mapped.get(task_id):
        raise ReadinessError('Tarefa desconhecida ou ainda não mapeada: ' + task_id)
    issue = mapped[task_id]
    if issue.get('state') != 'open':
        raise ReadinessError('A tarefa está fechada; não pode ser assumida.')
    if 'external-blocker' in label_names(issue):
        raise ReadinessError('A tarefa possui bloqueio externo.')
    unmet = [dep for dep in tasks[task_id].get('depends_on', []) if not delivered(mapped.get(dep))]
    if unmet:
        raise ReadinessError('Dependências ainda não integradas: ' + ', '.join(unmet))
    owners = assignee_names(issue)
    if owners - {user.lower()}:
        raise ReadinessError('A tarefa já pertence a outra pessoa.')
    if 'status:review' in label_names(issue):
        raise ReadinessError('A tarefa está em revisão; não reinicie sua execução por --claim.')
    occupied = [other_id for other_id, other in mapped.items()
                if other_id != task_id and other and other.get('state') == 'open'
                and user.lower() in assignee_names(other)]
    if occupied:
        raise ReadinessError('WIP máximo de uma tarefa por pessoa. Ainda atribuídas: ' + ', '.join(occupied))
    return issue


def claim_task(client, task_id, user, tasks, state):
    # Reconsultar imediatamente antes de escrever, inclusive se a CLI tinha um snapshot anterior.
    mapped = mapped_issues(tasks, state, fetch_issues(client))
    issue = validate_claim(task_id, user, tasks, mapped)
    newly_assigned = user.lower() not in assignee_names(issue)
    if newly_assigned:
        client.request(f"repos/{REPO}/issues/{issue['number']}/assignees", {'assignees': [user]}, 'POST')
    try:
        # A API não oferece transação de WIP entre issues; detectar uma disputa e desfazer só esta atribuição.
        fresh = mapped_issues(tasks, state, fetch_issues(client))
        if user.lower() not in assignee_names(fresh.get(task_id) or {}):
            raise ReadinessError('O GitHub não confirmou a atribuição; verifique o acesso da pessoa ao repositório.')
        validate_claim(task_id, user, tasks, fresh)
    except ReadinessError:
        if newly_assigned:
            client.request(f"repos/{REPO}/issues/{issue['number']}/assignees", {'assignees': [user]}, 'DELETE')
        raise
    set_status(client, fresh[task_id], 'doing')
    return {'task': task_id, 'user': user, 'number': issue['number'], 'status': 'doing'}


def synchronize(client, tasks, state, apply=False, project_hook=None):
    mapped = mapped_issues(tasks, state, fetch_issues(client))
    recovery = recover_integrations(client, tasks, state, mapped, apply=apply)
    if apply:
        # O histórico pode levar tempo; não escrever labels a partir do snapshot anterior.
        mapped = mapped_issues(tasks, state, fetch_issues(client))
    stale = remove_stale_integration(client, mapped) if apply else []
    readiness = calculate(tasks, mapped)
    changes = {}
    if apply:
        for task_id, detail in readiness.items():
            if mapped.get(task_id):
                changed = set_status(client, mapped[task_id], detail['status'])
                if changed:
                    changes[task_id] = changed
    project_result = 'Não há Project configurado; rótulos de issues são o quadro disponível.'
    if state.get('project'):
        hook = project_hook or getattr(github_setup, 'sync_project', None)
        if not apply:
            project_result = 'Somente consulta: não foram alterados campos do Project.'
        elif hook is None:
            project_result = 'Project configurado, mas sync_project não está disponível; sincronização ignorada explicitamente.'
        else:
            statuses = {task_id: detail['status'] for task_id, detail in readiness.items()}
            project_result = hook(client, state, statuses)
    return {'mode': 'apply' if apply else 'check', 'tasks': readiness, 'labelChanges': changes,
            'integrationRecovery': recovery,
            'staleIntegrationRemoved': stale, 'project': project_result,
            'nativeDependencies': 'O DAG de Data/backlog.json é a fonte de verdade; dependências nativas não foram consultadas.'}


def read_event():
    event_path = os.environ.get('GITHUB_EVENT_PATH')
    return (os.environ.get('GITHUB_EVENT_NAME', ''),
            json.loads(Path(event_path).read_text(encoding='utf-8')) if event_path else {})


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group()
    action.add_argument('--check', action='store_true', help='Consultar sem escrever no GitHub; é o padrão.')
    action.add_argument('--apply', action='store_true', help='Atualizar rótulos e, quando disponível, o Project.')
    action.add_argument('--claim', metavar='ID', help='Assumir uma tarefa se as dependências e o limite de WIP permitirem.')
    parser.add_argument('--user', metavar='LOGIN', help='Login GitHub da pessoa que assumirá a tarefa.')
    args = parser.parse_args(argv)
    if bool(args.claim) != bool(args.user):
        parser.error('--claim ID e --user LOGIN devem ser usados juntos.')
    try:
        tasks, state = load_local()
        client = Client()
        repo = client.request('repos/' + REPO)
        if repo.get('private') is not EXPECTED_PRIVATE or repo.get('full_name') != REPO:
            raise ReadinessError('O destino remoto deve ser o repositório e a visibilidade autorizados.')
        event_result = None
        if args.apply:
            name, event = read_event()
            event_result = merged_pr_event(client, name, event, tasks, state)
        claim_result = claim_task(client, args.claim, args.user, tasks, state) if args.claim else None
        result = synchronize(client, tasks, state, apply=bool(args.apply or args.claim))
        result.update(event=event_result, claim=claim_result)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (ReadinessError, RuntimeError, OSError, ValueError, KeyError) as error:
        print('Não foi possível concluir a operação: ' + str(error), file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
