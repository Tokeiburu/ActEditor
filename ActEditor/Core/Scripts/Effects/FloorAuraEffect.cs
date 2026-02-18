using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using TokeiLibrary;
using Utilities;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts.Effects {
	public class FloorAuraEffect : ImageProcessingEffect {
		public override string Group => "Effects/Global";
		public override string Image => "effect_floor_aura.png";

		public class GradientMaker {
			private List<(double Position, GrfColor Color)> _markers = new List<(double, GrfColor)>();

			public void AddColor(GrfColor color, double position) {
				_markers.Add((position, color));
			}

			public GrfColor GetColor(double position) {
				if (position > 1.0)
					return new GrfColor(0, 0, 0, 0);

				for (int i = 1; i < _markers.Count; i++) {
					if (position >= _markers[i - 1].Position && position <= _markers[i].Position) {
						var length = _markers[i].Position - _markers[i - 1].Position;
						var positionSub = (position - _markers[i - 1].Position) / length;

						return new GrfColor(
							(byte)((_markers[i].Color.A - _markers[i - 1].Color.A) * positionSub + _markers[i - 1].Color.A),
							(byte)((_markers[i].Color.R - _markers[i - 1].Color.R) * positionSub + _markers[i - 1].Color.R),
							(byte)((_markers[i].Color.G - _markers[i - 1].Color.G) * positionSub + _markers[i - 1].Color.G),
							(byte)((_markers[i].Color.B - _markers[i - 1].Color.B) * positionSub + _markers[i - 1].Color.B));
					}
				}

				return new GrfColor(0, 0, 0, 0);
			}

			public void Clear() {
				_markers.Clear();
			}
		}

		public class EffectOptions {
			public int Width;
			public int Height;
			public float MaxOpacity;
			public float MinOpacity;
			public List<GradientMaker> Gradients = new List<GradientMaker>();
		}

		private EffectOptions _options = new EffectOptions();

		public FloorAuraEffect() : base("Floor aura [Tokei]") {
			_inputGesture = "{Dialog.AnimationFloorAuraEffect}";
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);

			effect.AddProperty("Width", 250, 1, 400);
			effect.AddProperty("Height", 105, 1, 300);
			effect.AddProperty("MaxOpacity", 1f, 0f, 1f);
			effect.AddProperty("MinOpacity", 0.1f, 0f, 1f);

			_animationComponent.SetEditType(AnimationEditTypes.TargetOnly);
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);

			_options.Width = effect.GetProperty<int>("Width");
			_options.Height = effect.GetProperty<int>("Height");
			_options.MaxOpacity = effect.GetProperty<float>("MaxOpacity");
			_options.MinOpacity = effect.GetProperty<float>("MinOpacity");

			_options.Gradients.Clear();

			var image = new GrfImage(ApplicationManager.GetResource("gradient.png"));

			var transparentGradient = new GradientMaker();
			transparentGradient.AddColor(new GrfColor(0, 0, 0, 0), 0.0);
			transparentGradient.AddColor(new GrfColor(0, 0, 0, 0), 1.0);

			_options.Gradients.Add(transparentGradient);

			for (int x = 0; x < image.Width; x++) {
				var gradientMaker = new GradientMaker();
				_options.Gradients.Add(gradientMaker);

				for (int y = 0; y < image.Height; y++) {
					gradientMaker.AddColor(image.GetColor(x, y), (double)y / (image.Height - 1));
				}
			}
		}

		public override void OnBackupCommand(EffectConfiguration effect) {
			_transformedSprites2.Clear();
		}

		protected Dictionary<float, SpriteIndex> _transformedSprites2 = new Dictionary<float, SpriteIndex>();

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animStart = 0;
			animLength = action.Frames.Count;

			int[] steps = new int[animLength];
			float maxStep = 0;

			maxStep = animLength / 2;

			for (int i = 0; i < animLength / 2; i++) {
				steps[i] = i + 1;
				steps[animLength - i - 1] = i;
			}

			if (animLength % 2 == 1) {
				steps[animLength / 2] = steps[animLength / 2 - 1];
			}

			for (int i = 0; i < animLength; i++) {
				Frame frame = action[i];
				float mult = steps[i] / maxStep;

				SpriteIndex newSpriteIndex;

				if (!_transformedSprites2.TryGetValue(mult, out newSpriteIndex)) {
					newSpriteIndex = act.Sprite.InsertAny(_generateImage(mult));
					_transformedSprites2[mult] = newSpriteIndex;
				}

				Layer layer = new Layer(newSpriteIndex);
				layer.OffsetY = -1;
				frame.Layers.Insert(_applyLayerIndexes == null ? 0 : Math.Min(_applyLayerIndexes.First(), frame.Layers.Count), layer);
			}
		}

		private GrfImage _generateImage(float mult) {
			int width = _options.Width;
			int height = _options.Height;

			GrfImage image = new GrfImage(new byte[width * height * 4], width, height, GrfImageType.Bgra32);

			var min = Methods.Clamp(_options.MinOpacity, 0, 1);
			var max = Methods.Clamp(_options.MaxOpacity, 0, 1);

			if (min > max) {
				var t = min;
				min = max;
				max = t;
			}

			var diff = max - min;

			mult = mult * diff + min;

			_applyGradient2(image, mult);
			return image;
		}

		private void _applyGradient2(GrfImage image, double mult) {
			int width = image.Width;
			int height = image.Height;

			int bIdx = (int)Math.Min(_options.Gradients.Count - 2, (mult * (_options.Gradients.Count - 1)));

			var gradientMaker0 = _options.Gradients[bIdx];
			var gradientMaker1 = _options.Gradients[bIdx + 1];
			var bracketLow = (double)bIdx / (_options.Gradients.Count - 1);
			var bracketHigh = (double)(bIdx + 1) / (_options.Gradients.Count - 1);
			mult = (mult - bracketLow) / (bracketHigh - bracketLow);

			TkVector2 center = new TkVector2(0.5, 0.5);

			for (int x = 0; x < width; x++) {
				for (int y = 0; y < height; y++) {
					TkVector2 current = new TkVector2((float)x / width, (float)y / height);

					var position = (current - center).Length * 2;
					var color0 = gradientMaker0.GetColor(position);
					var color1 = gradientMaker1.GetColor(position);

					var color = new GrfColor(
						(byte)((color1.A - color0.A) * mult + color0.A),
						(byte)((color1.R - color0.R) * mult + color0.R),
						(byte)((color1.G - color0.G) * mult + color0.G),
						(byte)((color1.B - color0.B) * mult + color0.B)
					);

					image.SetColor(x, y, color);
				}
			}
		}

		public override void EnsureFrameCount(Action action, int animStart, int animLength, bool loopMissingFrames) {

		}
	}
}
