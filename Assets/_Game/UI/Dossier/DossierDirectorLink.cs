using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Liga as regras do dossiê (<see cref="DossierFlow"/>) ao <see cref="ChapterDirector"/> real. É a única classe
    /// que pede <c>Begin</c>; por isso o caminho "Iniciar" é um só, com duas travas: a do fluxo (clique no mesmo
    /// quadro) e a do diretor (Loading/Unloading ignoram <c>Begin</c>). Sem MonoBehaviour para ser testada no
    /// EditMode com um diretor real e um carregador falso.
    /// </summary>
    public sealed class DossierDirectorLink : IDisposable
    {
        readonly ChapterDirector director;
        readonly ChapterCatalog catalog;
        bool disposed;

        public DossierDirectorLink(ChapterDirector director, ChapterCatalog catalog, string languageCode)
        {
            this.director = director != null ? director : throw new ArgumentNullException(nameof(director));
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));

            var errors = catalog.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Catálogo inválido:" + ContractValidation.Describe(errors), nameof(catalog));

            var folders = new List<DossierFolderInfo>();
            foreach (var entry in catalog.Entries)
                folders.Add(new DossierFolderInfo(entry.SlotId, entry.IsAvailable));

            Flow = new DossierFlow(folders, languageCode);
            director.StateChanged += OnStateChanged;
            director.LoadFailed += OnLoadFailed;
            Flow.OnAppState(Map(director.State));
        }

        public DossierFlow Flow { get; }

        /// <summary>O diretor aceitou o início (já em Loading/Playing). É o momento do gesto da pessoa: liberar áudio e cursor.</summary>
        public event Action<ChapterCatalog.Entry> StartAccepted;

        /// <summary>Clique em Iniciar. Só <see cref="StartResult.Accepted"/> chegou ao <c>ChapterDirector.Begin</c>.</summary>
        public StartResult Start(string slotId)
        {
            var result = Flow.TryStart(slotId);
            if (result != StartResult.Accepted)
                return result;

            var entry = catalog.Find(slotId);
            try
            {
                director.Begin(entry.Chapter);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Flow.ReleaseStart(exception.Message);
                return StartResult.NotAvailable;
            }

            if (director.CanBegin)
            {
                // Begin foi ignorado (o diretor estava noutro estado): não deixa o botão preso.
                Flow.ReleaseStart();
                return StartResult.Busy;
            }

            StartAccepted?.Invoke(entry);
            return StartResult.Accepted;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (director != null)
            {
                director.StateChanged -= OnStateChanged;
                director.LoadFailed -= OnLoadFailed;
            }
        }

        void OnStateChanged(AppState state) => Flow.OnAppState(Map(state));

        void OnLoadFailed(string message)
        {
            Debug.LogWarning($"[DossierDirectorLink] {message}");
            Flow.OnLoadFailed(message);
        }

        static DossierAppState Map(AppState state) => (DossierAppState)(int)state;
    }
}
