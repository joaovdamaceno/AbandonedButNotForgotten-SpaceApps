# Editor — assembly `Abandoned.SpaceApps.Editor`

Asmdef: CORE-001. `SpaceAppsSceneBuilder.cs`, `SpaceAppsBuild.cs` e `Tools/build_release.py`: CORE-006 (R1) — **implementado**.

## Builders de cena por dono (`SpaceAppsSceneBuilder`)

Cada capítulo cria seu builder numa pasta `Editor` do capítulo (com `.asmref` para este assembly), herdando `SceneBuilderModule`:

```csharp
public sealed class MarsEnvironmentBuilder : SceneBuilderModule
{
    public override string Id => "mars.environment";
    public override string Owner => "R4 · MAR-012";
    public override string ScenePath => "Assets/_Game/Chapters/Mars/Scenes/Mars.unity";
    public override string RootName => "[R4] Environment";
    public override string AssetFolder => "Assets/_Game/Chapters/Mars/Art/Environment";

    public override void Build(SceneBuildContext context)
    {
        var ground = context.Child(context.Root.transform, "Ground");
        context.Component<MeshRenderer>(ground).sharedMaterial =
            context.Material(AssetFolder + "/Regolith.mat", "Universal Render Pipeline/Lit");
    }
}
```

- O builder recebe só a **sua raiz** e só pode criar assets em **`AssetFolder`**. Se alterar a raiz de outro dono ou escrever fora da pasta, nada é salvo e a cena é recarregada do disco. Vários donos podem dividir a mesma cena, cada um com a sua raiz.
- Use sempre `Child`, `Component`, `Asset`, `Material`, `Prefab` e `PrefabInstance` (achar ou criar): reconstruir não troca GUIDs nem fileIDs, e a segunda execução sem mudanças deixa os arquivos idênticos.
- Antes de salvar, a hierarquia é validada: scripts ausentes, renderer sem material ou com shader inválido, referência serializada perdida. `Validate` do builder acrescenta checagens próprias (chaves de texto, eventos terminais).
- Menus: **Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas / Listar builders**. Linha de comando: `-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine [-builders id1,id2]`.

## Build (`SpaceAppsBuild` + `Tools/build_release.py`)

```text
python Tools/build_release.py --targets windows,webgl                       # cenas das Build Settings
python Tools/build_release.py --targets webgl --scenes "A.unity;B.unity"     # cenas explícitas
python Tools/build_release.py --preflight-only --targets webgl              # só verificar
python Tools/build_release.py --rebuild-scenes --targets webgl              # builders antes do build
```

- Uma execução da Unity por alvo, **em sequência**, aberta já com `-buildTarget`: a troca de plataforma termina antes do `BuildPlayer` (é o que evita remover shaders por engano). Pelo menu (**CORE-006 → Build WebGL/Windows**) o Editor troca o alvo, espera importação e recarga do domínio e retoma sozinho.
- Pré-verificação: configuração do projeto (CORE-001), dados de contrato (CORE-002), cenas e hierarquias, pipeline efetivo da plataforma e, no WebGL, o perfil Leve da QA-001 em `Assets/_Game/Settings/Web` quando existir (sem ele, aviso e base do CORE-001).
- Pós-build: todo shader de que as cenas dependem precisa estar empacotado; `space-apps-build.json` na pasta da build lista arquivos reais, tamanhos e SHA-256, commit, pipeline e duração. O script recalcula os hashes e grava `Builds/release-summary-<data>.json`. `Builds/` é ignorada pelo Git.
- Release, sem Development; compressão, template e cabeçalhos do WebGL são da QA-001.
