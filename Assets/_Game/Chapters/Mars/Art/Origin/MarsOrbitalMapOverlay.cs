using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Painel do mapa orbital que a abertura mostra sobre o laboratório (Meridiani/hematita e Gusev). A UI (R2) desenha a imagem com
    /// <see cref="Opacity"/> e a linha de crédito pela chave; o diretor só informa o estado no relógio da sequência.
    /// </summary>
    public readonly struct MarsOrbitalMapOverlay
    {
        public readonly Texture2D Image;
        public readonly string CreditKey;
        public readonly float Opacity;

        public MarsOrbitalMapOverlay(Texture2D image, string creditKey, float opacity)
        {
            Image = image;
            CreditKey = creditKey;
            Opacity = opacity;
        }

        public bool IsVisible => Image != null && Opacity > 0.001f;
    }
}
