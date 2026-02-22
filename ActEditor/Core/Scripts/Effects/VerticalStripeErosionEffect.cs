using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts.Effects {
	public class VerticalStripeErosion : ImageProcessingEffect {
		public override string Image => "effect_vertical_stripe.png";
		public override string Group => @"Effects/Dead";

		public class EffectOptions {
			public int Part1EaseErosion;
			public float Part1TimeOffset;
			public int Part2EaseErosion;
			public float Part2TimeOffset;
			public int WhiteIntensity;
			public int RngSeed = 1234;
			public Random Rng;
		}

		public class Stripe {
			public int StartOffsetY = -1;
			public int OffsetY;
			public int Start;
			public bool Smear;
			public int PixelCount;
		}

		private EffectOptions _options = new EffectOptions();
		private ActIndex _index;
		private List<List<Stripe>> _layer2Stripes;
		private List<int> _layer2Offsets;
		private List<List<int>> _layer2BaseOffsetsY;
		private Dictionary<ActIndex, BoundingBox> _layer2BoundingBox;
		private HashSet<ActIndex> _processedActIndexes;

		public VerticalStripeErosion() : base("Vertical stripe erosion") {
			_inputGesture = "{Dialog.AnimationStripeErosion}";
			_generateBgra32Images = true;
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Part1EaseErosion", 10, -50, 50);
			effect.AddProperty("Part2EaseErosion", 10, -50, 50);
			effect.AddProperty("Part1TimeOffset", 0f, -1f, 1f);
			effect.AddProperty("Part2TimeOffset", -0.2f, -1f, 1f);
			effect.AddProperty("WhiteIntensity", 130, 0, 255);
			effect.AddProperty("RngSeed", 1234, 0, 10000);

			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.SetAnimation(4);
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Part1EaseErosion = effect.GetProperty<int>("Part1EaseErosion");
			_options.Part1TimeOffset = effect.GetProperty<float>("Part1TimeOffset");
			_options.Part2EaseErosion = effect.GetProperty<int>("Part2EaseErosion");
			_options.Part2TimeOffset = effect.GetProperty<float>("Part2TimeOffset");
			_options.WhiteIntensity = effect.GetProperty<int>("WhiteIntensity");
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));
		}

		public override void OnPreviewProcessAction(Act act, Action action, int aid) {
			base.OnPreviewProcessAction(act, action, aid);
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animLength = animLength <= 0 ? action.Frames.Count : animLength;

			int aid = act.Actions.IndexOf(action);

			_layer2Stripes = new List<List<Stripe>>();
			_layer2Offsets = new List<int>();
			_layer2BaseOffsetsY = new List<List<int>>();
			_layer2BoundingBox = new Dictionary<ActIndex, BoundingBox>();

			int maxLayerCount = action.Frames.Max(p => p.Layers.Count);

			for (int i = 0; i < maxLayerCount; i++) {
				if (!IsLayerForProcess(i)) {
					_layer2Stripes.Add(null);
					_layer2Offsets.Add(0);
					continue;
				}

				_layer2BaseOffsetsY.Add(new List<int>());

				int w = 0;
				int previousOffsetY = -1;

				BoundingBox layerBox = null;

				for (int j = animStart; j < animStart + animLength; j++) {
					Frame frame = action[j];

					if (i < frame.Layers.Count) {
						var layer = frame[i];
						var image = act.Sprite.GetImage(layer);

						if (image == null)
							continue;

						var tLayer = layer.Clone();
						tLayer.ScaleX = 1;
						tLayer.ScaleY = 1;
						var box = ActImaging.Imaging.GenerateFrameBoundingBox(act, tLayer);
						_layer2BoundingBox[new ActIndex { ActionIndex = aid, FrameIndex = j, LayerIndex = i }] = box;

						if (layerBox == null)
							layerBox = box;
						else
							layerBox += box;
					}
				}

				if (layerBox == null) {
					_layer2Stripes.Add(null);
					_layer2Offsets.Add(0);
					continue;
				}

				var stripes = new List<Stripe>();
				w = (int)Math.Ceiling(layerBox.Max[0] - layerBox.Min[0]);

				for (int x = 0; x < w; x++) {
					Stripe stripe = new Stripe();

					stripe.Smear = x % 2 == 1;
					stripe.Start = 0;

					do {
						stripe.OffsetY = _options.Rng.Next(-30, 0);
					}
					while (stripe.OffsetY == previousOffsetY);

					stripes.Add(stripe);
				}

				for (int j = animStart; j < animStart + animLength; j++) {
					Frame frame = action[j];

					if (i < frame.Layers.Count) {
						var layer = frame[i];
						var image = act.Sprite.GetImage(layer);

						if (image == null)
							continue;

						var box = _layer2BoundingBox[new ActIndex { ActionIndex = aid, FrameIndex = j, LayerIndex = i }];
						var offsetX = (int)layerBox.Min.X;

						for (int x = 0; x < image.Width; x++) {
							for (int y = image.Height - 1; y >= 0; y--) {
								if (!image.IsPixelTransparent(x, y)) {
									int ty = image.Height - y - 1;
									stripes[x + (int)box.Min.X - offsetX].StartOffsetY = Math.Max(ty, stripes[x + (int)box.Min.X - offsetX].StartOffsetY);
									break;
								}
							}
						}
					}
				}

				_layer2Stripes.Add(stripes);
				_layer2Offsets.Add((int)layerBox.Min.X);
			}

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];

					_index = new ActIndex() { ActionIndex = aid, FrameIndex = i, LayerIndex = layerIndex };
					_processedActIndexes.Add(_index);

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor.Value);

					ProcessLayer(act, layer, step, animLength);
				}
			}
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			var sprIndex = layer.SprSpriteIndex;

			if (sprIndex.Valid) {
				SpriteIndex newSpriteIndex;

				if (!_transformedSprites.TryGetValue((sprIndex, step), out newSpriteIndex)) {
					var image = act.Sprite.GetImage(sprIndex).Copy();

					image.Convert(GrfImageType.Bgra32);
					int height = image.Height / 2;
					height = height + height % 2;
					image.Margin(0, height, 0, 0);

					newSpriteIndex = act.Sprite.InsertAny(image);
					ProcessImage(image, step, animLength);
					_transformedSprites[(sprIndex, step)] = newSpriteIndex;
				}

				layer.SprSpriteIndex = newSpriteIndex;

				{
					var image = act.Sprite.GetImage(sprIndex);
					int height = image.Height / 2;
					height = height + height % 2;
					layer.OffsetY -= height / 2;
				}
			}
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			// Lighten image
			int lightIncrease = _options.WhiteIntensity;
			float excessMult = 1f;

			img.Add(new GrfColor(255, (byte)lightIncrease, (byte)lightIncrease, (byte)lightIncrease));

			var adjustOffsetX = _layer2Offsets[_index.LayerIndex];
			var stripes = _layer2Stripes[_index.LayerIndex];
			var box = _layer2BoundingBox[_index];

			var offsetX = (int)box.Min.X;
			var part1Ease = InterpolationAnimation.GetEaseMethod(_options.Part1EaseErosion);
			var part2Ease = InterpolationAnimation.GetEaseMethod(_options.Part2EaseErosion);

			float t0 = (float)step / totalSteps + _options.Part1TimeOffset;
			t0 = part1Ease(t0) * excessMult;

			float t1 = (float)step / totalSteps + _options.Part2TimeOffset;
			t1 = part2Ease(t1) * excessMult;

			for (int x = 0; x < img.Width; x++) {
				var stripe = stripes[x + offsetX - adjustOffsetX];
				if (!stripe.Smear) {
					var end = stripe.StartOffsetY + (int)(img.Height * t0 + stripe.OffsetY);

					for (int y = 0; y < end && y < img.Height; y++) {
						int ty = img.Height - y - 1;
						img.SetPixelTransparent(x, ty);
					}
				}
				else {
					var end = stripe.StartOffsetY + (int)(img.Height * t1 + stripe.OffsetY);

					for (int y = 0; y < end && y < img.Height; y++) {
						int ty = img.Height - y - 1;
						img.SetPixelTransparent(x, ty);
					}

					// Next, smear pixels
					// Maximum smear of 1.5
					var end2 = Math.Min((int)(end * 1.50), img.Height);

					// Count how many pixels are available to smear
					int pixelsAvailable = 0;

					for (int y = end; y < end2 && y < img.Height; y++) {
						int ty = img.Height - y - 1;
						if (img.IsPixelTransparent(x, ty))
							continue;
						pixelsAvailable++;
					}

					end2 = (int)(end + pixelsAvailable * 1.5f);
					GrfColor smearColor;
					List<int> potentialColors = new List<int>();

					for (int y = end; y < end2 && y < img.Height; y++) {
						int ty = img.Height - y - 1;

						if (!img.IsPixelTransparent(x, ty)) {
							potentialColors.Add(ty);
						}
					}

					if (potentialColors.Count > 0) {
						smearColor = img.GetColor(x, potentialColors[_options.Rng.Next(potentialColors.Count)]);
						for (int y = end; y < end2 && y < img.Height; y++) {
							int ty = img.Height - y - 1;
							img.SetColor(x, ty, smearColor);
						}
					}
				}
			}
		}

		public override void OnBackupCommand(EffectConfiguration effect) {
			_processedActIndexes = new HashSet<ActIndex>();
		}

		public override void OnPostBackupCommand() {
			// Cleanup images...
			ActHelper.TrimImages(_actInput, _processedActIndexes.ToList(), 0x10, keepPerfectAlignment: true);

			for (int i = _actInput.Sprite.Images.Count - 1; i >= 0; i--) {
				var image = _actInput.Sprite.Images[i];

				if (image.Width == 1 && image.Height == 1 && image.IsPixelTransparent(0, 0)) {
					_actInput.Sprite.Remove(i, _actInput, EditOption.AdjustIndexes);
				}
			}
		}
	}
}
