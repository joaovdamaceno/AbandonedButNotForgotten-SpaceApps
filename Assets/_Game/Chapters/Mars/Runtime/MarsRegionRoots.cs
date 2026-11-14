using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Objetos da cena que pertencem a uma região (palco, atividades, ambiente): ativam e desativam juntos.</summary>
    [Serializable]
    public struct MarsRegionRoots
    {
        public string regionId;
        public List<GameObject> roots;
    }
}
