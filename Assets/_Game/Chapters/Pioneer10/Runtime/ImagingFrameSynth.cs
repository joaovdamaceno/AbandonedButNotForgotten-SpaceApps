using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 4 · as imagens do jogo da foto. Cada passagem do fotopolarímetro registra o planeta nos canais vermelho e azul,
    /// e cada registro isolado vem cheio de ruído; somar vários registros apaga o ruído e a imagem aparece. Este sintetizador gera,
    /// de forma determinística e sem asset externo, o planeta "verdadeiro" (faixas, Grande Mancha Vermelha, escurecimento do limbo) por
    /// canal, cada registro ruidoso (a verdade mais um ruído gaussiano com semente própria) e a média de vários registros.
    /// É uma ilustração: as faixas são desenhadas, não dados da Pioneer, e o ruído é de composição, não a calibração do instrumento.
    /// </summary>
    public static class ImagingFrameSynth
    {
        public const int Size = 128;
        /// <summary>Desvio-padrão do ruído de um registro (a verdade vai de 0 a 1).</summary>
        public const float NoiseSigma = 0.42f;

        static readonly Dictionary<ImagingChannel, float[]> BaseCache = new Dictionary<ImagingChannel, float[]>();
        static readonly Dictionary<(ImagingChannel, int), float[]> FrameCache = new Dictionary<(ImagingChannel, int), float[]>();

        /// <summary>O planeta sem ruído no canal, de 0 a 1, linhas de baixo para cima (como as texturas da Unity).</summary>
        public static float[] Truth(ImagingChannel channel)
        {
            if (BaseCache.TryGetValue(channel, out var cached))
                return cached;

            var values = new float[Size * Size];
            var red = channel == ImagingChannel.Red;
            const float radius = 0.80f;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var u = (x + 0.5f) / Size * 2f - 1f;
                    var v = ((y + 0.5f) / Size * 2f - 1f) * 1.07f; // Júpiter é achatado nos polos
                    var r2 = u * u + v * v;
                    var value = 0.03f;
                    if (r2 < radius * radius)
                    {
                        var lat = v / radius;
                        var lon = u / radius;
                        var bands = 0.5f + 0.27f * Mathf.Sin(lat * 17f + 0.55f * Mathf.Sin(lat * 5f))
                                         + 0.13f * Mathf.Sin(lat * 37f + 1.3f)
                                         + 0.06f * Mathf.Sin(lon * 9f + lat * 13f);
                        // A Grande Mancha Vermelha: escura no azul, só um pouco mais clara no vermelho.
                        var gx = (lon - 0.28f) / 0.19f;
                        var gy = (lat + 0.34f) / 0.10f;
                        var spot = Mathf.Exp(-(gx * gx + gy * gy));
                        float level;
                        if (red)
                            level = 0.64f + 0.95f * (bands - 0.5f) + 0.10f * spot;
                        else
                            level = 0.36f + 1.25f * (bands - 0.5f) - 0.32f * spot;
                        var mu = Mathf.Sqrt(Mathf.Max(0f, 1f - r2 / (radius * radius)));
                        value = Mathf.Clamp01(level * Mathf.Pow(mu, 0.45f)); // escurecimento do limbo
                        var edge = Mathf.Clamp01((radius - Mathf.Sqrt(r2)) * Size * 0.5f); // borda sem serrilhado
                        value = Mathf.Lerp(0.03f, value, edge);
                    }

                    values[y * Size + x] = value;
                }
            }

            BaseCache[channel] = values;
            return values;
        }

        /// <summary>O registro <paramref name="shot"/> (0, 1, …) do canal: a verdade mais ruído gaussiano. Sem recorte: a média precisa dos valores brutos.</summary>
        public static float[] Frame(ImagingChannel channel, int shot)
        {
            var key = (channel, shot);
            if (FrameCache.TryGetValue(key, out var cached))
                return cached;

            var truth = Truth(channel);
            var values = new float[truth.Length];
            var random = new System.Random(7919 * (shot + 1) + (channel == ImagingChannel.Red ? 17 : 331));
            for (var i = 0; i < values.Length; i++)
                values[i] = truth[i] + NoiseSigma * Gaussian(random);

            FrameCache[key] = values;
            return values;
        }

        /// <summary>A média dos primeiros <paramref name="count"/> registros do canal (o ruído cai com a raiz de <paramref name="count"/>).</summary>
        public static float[] Average(ImagingChannel channel, int count)
        {
            count = Mathf.Max(1, count);
            var values = new float[Size * Size];
            for (var shot = 0; shot < count; shot++)
            {
                var frame = Frame(channel, shot);
                for (var i = 0; i < values.Length; i++)
                    values[i] += frame[i];
            }

            for (var i = 0; i < values.Length; i++)
                values[i] /= count;
            return values;
        }

        /// <summary>Desvio-padrão do ruído que sobra em <paramref name="values"/>, medido contra a verdade do canal.</summary>
        public static float NoiseOf(float[] values, ImagingChannel channel)
        {
            var truth = Truth(channel);
            var sum = 0.0;
            for (var i = 0; i < values.Length; i++)
            {
                var d = values[i] - truth[i];
                sum += d * d;
            }

            return (float)Math.Sqrt(sum / values.Length);
        }

        /// <summary>Um registro de um canal como imagem: tons de vermelho ou de azul (o canal também aparece no glifo do painel).</summary>
        public static Texture2D ChannelTexture(float[] values, ImagingChannel channel, string name)
        {
            var pixels = new Color32[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var v = Mathf.Clamp01(values[i]);
                pixels[i] = channel == ImagingChannel.Red
                    ? new Color(v, v * 0.30f, v * 0.20f)
                    : new Color(v * 0.26f, v * 0.50f, v);
            }

            return Build(pixels, name);
        }

        /// <summary>
        /// As duas imagens somadas numa só: o vermelho vai no canal vermelho e o azul no canal azul. O verde NÃO foi medido, por isso fica
        /// de fora (a cena explica que a terceira cor foi estimada na Terra).
        /// </summary>
        public static Texture2D CompositeTexture(float[] red, float[] blue, string name)
        {
            var pixels = new Color32[red.Length];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(Mathf.Clamp01(red[i]), 0f, Mathf.Clamp01(blue[i]));
            return Build(pixels, name);
        }

        static Texture2D Build(Color32[] pixels, string name)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        static float Gaussian(System.Random random)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }
}
