using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Âncoras locais para as tarefas SUR consumidoras. Não lê entrada nem avança missão.</summary>
    public sealed class SurveyorSiteBindings : MonoBehaviour
    {
        [Serializable]
        public sealed class Target
        {
            public string id;
            public Transform anchor;
        }

        [SerializeField] CameraDirector cameraDirector;
        [SerializeField] Transform cameraMount;
        [SerializeField] Transform mirrorAzimuth;
        [SerializeField] Transform mirrorElevation;
        [SerializeField] Transform scoopArm;
        [SerializeField] Transform scoop;
        [SerializeField] Target[] targets = Array.Empty<Target>();

        public CameraDirector CameraDirector => cameraDirector;
        public Transform CameraMount => cameraMount;
        public Transform MirrorAzimuth => mirrorAzimuth;
        public Transform MirrorElevation => mirrorElevation;
        public Transform ScoopArm => scoopArm;
        public Transform Scoop => scoop;
        public IReadOnlyList<Target> Targets => targets;

        public void Configure(CameraDirector director, Transform mount, Transform azimuth, Transform elevation,
            Transform arm, Transform tool, Target[] siteTargets)
        {
            cameraDirector = director; cameraMount = mount; mirrorAzimuth = azimuth; mirrorElevation = elevation;
            scoopArm = arm; scoop = tool; targets = siteTargets;
        }

        public bool TryGetTarget(string id, out Transform anchor)
        {
            foreach (var target in targets ?? Array.Empty<Target>())
                if (target != null && target.id == id) { anchor = target.anchor; return anchor != null; }
            anchor = null; return false;
        }

        void OnEnable()
        {
            if (!Application.isPlaying || cameraDirector == null) return;
            cameraDirector.RegisterRig("Surveyor.Site", transform);
            foreach (var target in targets ?? Array.Empty<Target>())
                if (target != null && target.anchor != null) cameraDirector.RegisterLookTarget(target.id, target.anchor);
        }

        void OnDisable()
        {
            if (!Application.isPlaying || cameraDirector == null) return;
            cameraDirector.UnregisterRig("Surveyor.Site");
            foreach (var target in targets ?? Array.Empty<Target>())
                if (target != null) cameraDirector.UnregisterLookTarget(target.id);
        }
    }
}
