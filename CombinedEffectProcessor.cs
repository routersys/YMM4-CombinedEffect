using System.Collections.Immutable;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CombinedEffect
{
    public class CombinedEffectProcessor : IVideoEffectProcessor
    {
        private readonly CombinedEffect item;
        private readonly IGraphicsDevicesAndContext devices;
        private List<IVideoEffectProcessor> processors = new();
        private ImmutableList<IVideoEffect> currentEffects = ImmutableList<IVideoEffect>.Empty;
        private bool isDisposed;
        private ID2D1Image? inputImage;

        public ID2D1Image? Output { get; private set; }

        public CombinedEffectProcessor(CombinedEffect item, IGraphicsDevicesAndContext devices)
        {
            this.item = item;
            this.devices = devices;
        }

        private void UpdateProcessors()
        {
            if (currentEffects.SequenceEqual(item.Effects))
            {
                return;
            }

            foreach (var processor in processors)
            {
                processor.Dispose();
            }
            processors.Clear();

            foreach (var effect in item.Effects)
            {
                processors.Add(effect.CreateVideoEffect(devices));
            }
            currentEffects = item.Effects;
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            if (isDisposed)
                return effectDescription.DrawDescription;

            UpdateProcessors();

            if (processors.Count == 0)
            {
                Output = inputImage;
                return effectDescription.DrawDescription;
            }

            ID2D1Image? currentImage = inputImage;
            var currentEffectDescription = effectDescription;

            foreach (var processor in processors)
            {
                processor.SetInput(currentImage);
                var newDrawDescription = processor.Update(currentEffectDescription);
                currentImage = processor.Output;

                currentEffectDescription = currentEffectDescription with { DrawDescription = newDrawDescription };
            }

            Output = currentImage;
            return currentEffectDescription.DrawDescription;
        }

        public void SetInput(ID2D1Image? input)
        {
            if (isDisposed)
                return;
            inputImage = input;
        }

        public void ClearInput()
        {
            if (isDisposed)
                return;
            inputImage = null;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            foreach (var processor in processors)
            {
                processor.Dispose();
            }
            processors.Clear();
            isDisposed = true;
        }
    }
}