using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Um builder de cena com dono. Cada capítulo cria sua subclasse (em uma pasta Editor do capítulo, com .asmref) e
    /// declara a cena, a raiz que lhe pertence e a pasta de assets em que pode escrever. O framework garante que o
    /// builder não toque raízes nem arquivos de outro dono, mantém GUIDs/fileIDs em reconstruções e valida antes de salvar.
    /// </summary>
    public abstract class SceneBuilderModule
    {
        /// <summary>ID estável, ex.: "mars.environment".</summary>
        public abstract string Id { get; }

        /// <summary>Função e tarefa responsáveis, ex.: "R4 · MAR-012".</summary>
        public abstract string Owner { get; }

        /// <summary>Cena alvo (criada se não existir), ex.: Assets/_Game/Chapters/Mars/Scenes/Mars.unity.</summary>
        public abstract string ScenePath { get; }

        /// <summary>Nome do objeto raiz que só este builder escreve, ex.: "[R4] Environment".</summary>
        public abstract string RootName { get; }

        /// <summary>Única pasta onde o builder pode criar ou alterar assets (prefabs, materiais…).</summary>
        public abstract string AssetFolder { get; }

        /// <summary>Monta ou atualiza a raiz. Use os métodos do contexto (achar-ou-criar) para manter IDs estáveis.</summary>
        public abstract void Build(SceneBuildContext context);

        /// <summary>Validações específicas (chaves de texto, eventos terminais…). Erros impedem salvar.</summary>
        public virtual void Validate(SceneBuildContext context, List<string> errors)
        {
        }
    }

    /// <summary>Resultado de uma reconstrução: salva ou não, com mudanças e erros legíveis.</summary>
    public sealed class SceneBuildResult
    {
        public string BuilderId;
        public string ScenePath;
        public bool Saved;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Changes = new List<string>();

        public bool Succeeded => Saved && Errors.Count == 0;

        public override string ToString() =>
            Succeeded
                ? $"[CORE-006] {BuilderId}: {ScenePath} salva ({Changes.Count} mudança(s))."
                : $"[CORE-006] {BuilderId}: NÃO salva.{ContractValidation.Describe(Errors)}";
    }

    /// <summary>
    /// API restrita entregue ao builder: só a raiz dele e a pasta de assets dele. Todas as operações são
    /// "achar ou criar" por nome/caminho, para reconstruir sem perder GUIDs nem referências.
    /// </summary>
    public sealed class SceneBuildContext
    {
        readonly SceneBuilderModule module;
        readonly SceneBuildResult result;

        internal SceneBuildContext(SceneBuilderModule module, Scene scene, GameObject root, SceneBuildResult result)
        {
            this.module = module;
            this.result = result;
            Scene = scene;
            Root = root;
        }

        public Scene Scene { get; }
        public GameObject Root { get; }
        public string Owner => module.Owner;

        /// <summary>Filho direto com este nome (o primeiro), criado se faltar.</summary>
        public GameObject Child(Transform parent, string name)
        {
            RequireInsideRoot(parent);
            var existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            result.Changes.Add("criado " + PathOf(go.transform));
            return go;
        }

        public T Component<T>(GameObject target) where T : Component
        {
            RequireInsideRoot(target.transform);
            var component = target.GetComponent<T>();
            if (component != null)
                return component;

            result.Changes.Add($"adicionado {typeof(T).Name} em {PathOf(target.transform)}");
            return target.AddComponent<T>();
        }

        /// <summary>Asset no caminho (mantém o GUID); criado por <paramref name="create"/> se faltar. Só dentro de AssetFolder.</summary>
        public T Asset<T>(string assetPath, Func<T> create) where T : Object
        {
            RequireOwnedPath(assetPath);
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
                return existing;

            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var asset = create();
            AssetDatabase.CreateAsset(asset, assetPath);
            result.Changes.Add("asset criado " + assetPath);
            return asset;
        }

        /// <summary>Material URP no caminho, com o shader pedido (o shader é reaplicado se mudou).</summary>
        public Material Material(string assetPath, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Shader '{shaderName}' não encontrado.");

            var material = Asset(assetPath, () => new Material(shader));
            if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>Cria ou atualiza um prefab no caminho, preservando o GUID e os fileIDs internos.</summary>
        public GameObject Prefab(string assetPath, Action<GameObject> configure)
        {
            RequireOwnedPath(assetPath);
            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            if (File.Exists(assetPath))
            {
                var contents = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    configure(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            else
            {
                var temp = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
                try
                {
                    configure(temp);
                    PrefabUtility.SaveAsPrefabAsset(temp, assetPath);
                    result.Changes.Add("prefab criado " + assetPath);
                }
                finally
                {
                    Object.DestroyImmediate(temp);
                }
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        /// <summary>Instância do prefab com este nome sob o pai; reaproveitada se já existe (mesmos fileIDs).</summary>
        public GameObject PrefabInstance(Transform parent, GameObject prefab, string name)
        {
            RequireInsideRoot(parent);
            var existing = parent.Find(name);
            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                return existing.gameObject;

            if (existing != null)
                throw new InvalidOperationException($"'{PathOf(existing)}' existe mas não é instância de {prefab.name}.");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            result.Changes.Add("instância criada " + PathOf(instance.transform));
            return instance;
        }

        void RequireInsideRoot(Transform target)
        {
            if (target == null || !(target == Root.transform || target.IsChildOf(Root.transform)))
                throw new InvalidOperationException($"O builder '{module.Id}' só pode escrever dentro de '{module.RootName}'.");
        }

        void RequireOwnedPath(string assetPath)
        {
            var folder = module.AssetFolder.TrimEnd('/') + "/";
            if (string.IsNullOrEmpty(assetPath) || !assetPath.Replace('\\', '/').StartsWith(folder, StringComparison.Ordinal))
                throw new InvalidOperationException($"O builder '{module.Id}' só pode escrever assets em {module.AssetFolder} (pedido: {assetPath}).");
        }

        internal static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            EnsureFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder)?.Replace('\\', '/'), Path.GetFileName(folder));
        }

        internal static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }

    /// <summary>
    /// CORE-006: executa os builders de cena com isolamento por dono e validação. Menus em
    /// <c>Abandoned Space Apps/CORE-006</c>; linha de comando:
    /// <c>-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsSceneBuilder.BuildFromCommandLine [-builders id1,id2]</c>.
    /// </summary>
    public static class SpaceAppsSceneBuilder
    {
        const string ErrorShaderName = "Hidden/InternalErrorShader";

        [MenuItem("Abandoned Space Apps/CORE-006/Reconstruir todas as cenas", priority = 40)]
        public static void BuildAllFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (var result in RunAll(null))
                Log(result);
        }

        [MenuItem("Abandoned Space Apps/CORE-006/Listar builders", priority = 41)]
        public static void ListFromMenu()
        {
            var modules = Discover(out var errors);
            var text = new StringBuilder("[CORE-006] Builders registrados:");
            foreach (var module in modules)
                text.Append($"\n- {module.Id} · {module.Owner} · {module.ScenePath} · raiz '{module.RootName}'");
            if (modules.Count == 0)
                text.Append(" nenhum (cada capítulo registra o seu ao criar a cena).");
            Debug.Log(text.ToString());
            foreach (var error in errors)
                Debug.LogError("[CORE-006] " + error);
        }

        public static void BuildFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var filter = ReadArgument("-builders");
                var ids = string.IsNullOrEmpty(filter) ? null : new HashSet<string>(filter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (var result in RunAll(ids))
                {
                    Log(result);
                    if (!result.Succeeded)
                        exitCode = 1;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>Builders concretos do projeto (assemblies de teste ficam de fora). IDs duplicados viram erro.</summary>
        public static List<SceneBuilderModule> Discover(out List<string> errors)
        {
            errors = new List<string>();
            var modules = new List<SceneBuilderModule>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<SceneBuilderModule>())
            {
                if (type.IsAbstract || type.Assembly.GetName().Name.Contains(".Tests") || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                modules.Add((SceneBuilderModule)Activator.CreateInstance(type));
            }

            errors.AddRange(CheckModules(modules));
            return modules.OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>IDs válidos e únicos; raízes únicas por cena; caminhos dentro de Assets/_Game.</summary>
        public static List<string> CheckModules(IReadOnlyList<SceneBuilderModule> modules)
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            var roots = new HashSet<string>();
            foreach (var module in modules)
            {
                var owner = $"Builder {module.GetType().Name}";
                ContractValidation.RequireId(errors, owner, nameof(module.Id), module.Id);
                ContractValidation.RequireText(errors, owner, nameof(module.Owner), module.Owner);
                ContractValidation.RequireText(errors, owner, nameof(module.RootName), module.RootName);
                if (module.ScenePath == null || !module.ScenePath.StartsWith("Assets/_Game/", StringComparison.Ordinal) || !module.ScenePath.EndsWith(".unity", StringComparison.Ordinal))
                    ContractValidation.Report(errors, owner, $"ScenePath deve ser uma .unity dentro de Assets/_Game (atual: {module.ScenePath}).");
                if (module.AssetFolder == null || !module.AssetFolder.StartsWith("Assets/_Game/", StringComparison.Ordinal))
                    ContractValidation.Report(errors, owner, $"AssetFolder deve ficar dentro de Assets/_Game (atual: {module.AssetFolder}).");
                if (module.Id != null && !ids.Add(module.Id))
                    ContractValidation.Report(errors, owner, $"ID '{module.Id}' duplicado.");
                if (module.ScenePath != null && module.RootName != null && !roots.Add(module.ScenePath + "|" + module.RootName))
                    ContractValidation.Report(errors, owner, $"raiz '{module.RootName}' já pertence a outro builder em {module.ScenePath}.");
            }

            return errors;
        }

        public static List<SceneBuildResult> RunAll(ISet<string> onlyIds)
        {
            var modules = Discover(out var errors);
            if (errors.Count > 0)
                throw new InvalidOperationException("Builders inválidos:" + ContractValidation.Describe(errors));

            return modules.Where(m => onlyIds == null || onlyIds.Contains(m.Id)).Select(Run).ToList();
        }

        /// <summary>
        /// Abre (ou cria) a cena, entrega ao builder só a raiz dele, valida e salva. Se o builder alterar outra raiz ou a
        /// validação falhar, nada é salvo e a cena é recarregada do disco.
        /// </summary>
        public static SceneBuildResult Run(SceneBuilderModule module)
        {
            var result = new SceneBuildResult { BuilderId = module.Id, ScenePath = module.ScenePath };
            var moduleErrors = CheckModules(new[] { module });
            if (moduleErrors.Count > 0)
            {
                result.Errors.AddRange(moduleErrors);
                return result;
            }

            var scene = OpenOrCreate(module.ScenePath, result);
            var others = FingerprintOtherRoots(scene, module.RootName);
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == module.RootName);
            if (root == null)
            {
                root = new GameObject(module.RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                result.Changes.Add("raiz criada " + module.RootName);
            }

            var context = new SceneBuildContext(module, scene, root, result);
            try
            {
                module.Build(context);
            }
            catch (Exception exception)
            {
                result.Errors.Add($"{module.Id}: falha ao construir: {exception.Message}");
            }

            if (FingerprintOtherRoots(scene, module.RootName) != others)
                result.Errors.Add($"{module.Id}: alterou objetos fora de '{module.RootName}' (de outro dono). Nada foi salvo.");

            if (result.Errors.Count == 0)
            {
                result.Errors.AddRange(ValidateHierarchy(root));
                module.Validate(context, result.Errors);
            }

            if (result.Errors.Count > 0)
            {
                EditorSceneManager.OpenScene(module.ScenePath, OpenSceneMode.Single); // descarta as mudanças em memória
                AssetDatabase.Refresh();
                return result;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            result.Saved = EditorSceneManager.SaveScene(scene, module.ScenePath);
            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Scripts ausentes, renderers sem material/shader válido e referências serializadas quebradas.</summary>
        public static List<string> ValidateHierarchy(GameObject root)
        {
            var errors = new List<string>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var go = transform.gameObject;
                var path = SceneBuildContext.PathOf(transform);
                var missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missingScripts > 0)
                    errors.Add($"{path}: {missingScripts} script(s) ausente(s).");

                foreach (var renderer in go.GetComponents<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    if (materials.Length == 0)
                        errors.Add($"{path}: {renderer.GetType().Name} sem material.");

                    for (var i = 0; i < materials.Length; i++)
                    {
                        var material = materials[i];
                        if (material == null)
                            errors.Add($"{path}: material {i} ausente.");
                        else if (material.shader == null || material.shader.name == ErrorShaderName || ShaderUtil.ShaderHasError(material.shader))
                            errors.Add($"{path}: material '{material.name}' com shader inválido.");
                    }
                }

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null)
                        continue;

                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference
                            && property.objectReferenceValue == null
                            && property.objectReferenceEntityIdValue != EntityId.None)
                        {
                            errors.Add($"{path}: {component.GetType().Name}.{property.propertyPath} aponta para objeto perdido.");
                        }
                    }
                }
            }

            return errors;
        }

        static Scene OpenOrCreate(string scenePath, SceneBuildResult result)
        {
            if (File.Exists(scenePath))
                return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            SceneBuildContext.EnsureFolder(Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
            result.Changes.Add("cena criada " + scenePath);
            return scene;
        }

        /// <summary>Impressão digital barata das raízes de outros donos: hierarquia, componentes e transforms.</summary>
        static string FingerprintOtherRoots(Scene scene, string ownRoot)
        {
            var text = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == ownRoot)
                    continue;

                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    text.Append(SceneBuildContext.PathOf(transform)).Append('|')
                        .Append(transform.gameObject.activeSelf).Append('|')
                        .Append(transform.localPosition).Append(transform.localRotation).Append(transform.localScale).Append('|');
                    foreach (var component in transform.GetComponents<Component>())
                        text.Append(component == null ? "null" : component.GetType().Name).Append(',');
                    text.Append('\n');
                }
            }

            return text.ToString();
        }

        static void Log(SceneBuildResult result)
        {
            if (result.Succeeded)
                Debug.Log(result.ToString());
            else
                Debug.LogError(result.ToString());
        }

        internal static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return null;
        }
    }
}
