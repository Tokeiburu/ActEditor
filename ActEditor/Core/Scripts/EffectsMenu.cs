using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using GRF.IO;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;
using Utilities.IndexProviders;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace ActEditor.Core.Scripts {
	public class EffectFadeAnimation : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public GrfColor TargetColor;
			public int Ease;
		}

		private EffectOptions _options = new EffectOptions();
		private Func<float, float> _easeMethod;

		public EffectFadeAnimation() : base("Fade/Target color") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("TargetColor", new GrfColor(0, 255, 255, 255), null, null);
			effect.AddProperty("Ease", 10, -50, 50);

			_animationComponent.DefaultSaveData.SetAnimation(4);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.TargetColor = effect.GetProperty<GrfColor>("TargetColor");
			_options.Ease = effect.GetProperty<int>("Ease");

			_easeMethod = InterpolationAnimation.GetEaseMethod(_options.Ease);
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			float t = (float)step / animLength;
			t = _easeMethod(t);

			var lColor = layer.Color.ToTkVector4();

			layer.Color = ((_options.TargetColor.ToTkVector4() - lColor) * t + lColor).ToGrfColor();
		}

		public override string Group => "Effects/Global";
		public override string InputGesture => "{Dialog.AnimationFade|Ctrl-Alt-T}";
		public override string Image => "fade.png";

		#endregion
	}

	public class CutSpriteEffect : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public TkVector2 Source;
			public float Angle;
			public float HalfAngle;
			public int RngSeed;
			public Random Rng;
		}

		private EffectOptions _options = new EffectOptions();

		private List<GrfColor> _borderColors = new List<GrfColor>();
		private List<byte> _borderColorsIndexes = new List<byte>();
		private BoundingBox _actionBox;
		private bool[,] _cutMask;
		private int[,] _noise;

		public CutSpriteEffect() : base("Cut sprite") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Source", new TkVector2(0, 14), new TkVector2(-100, 100), new TkVector2(-100, 100));
			effect.AddProperty("Angle", 0, -360f, 360f);
			effect.AddProperty("HalfAngle", 24f, 0, 90f);
			effect.AddProperty("RngSeed", 1234, 0, 10000);

			_animationComponent.DefaultSaveData.SetAnimation(4);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();

			_borderColors.Add(new GrfColor(255, 255, 0, 0));
			_borderColors.Add(new GrfColor(255, 200, 0, 0));
			_borderColors.Add(new GrfColor(255, 150, 0, 0));
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Source = effect.GetProperty<TkVector2>("Source");
			_options.Angle = effect.GetProperty<float>("Angle");
			_options.HalfAngle = effect.GetProperty<float>("HalfAngle");
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));

			_borderColorsIndexes.Clear();
			
			foreach (var color in _borderColors) {
				_borderColorsIndexes.Add((byte)_addPaletteColor(color));
			}
		}

		public override void OnPreviewProcessAction(Act act, Action action, int aid) {
			_actionBox = ActImaging.Imaging.GenerateBoundingBox(act, aid);
			int w = (int)(_actionBox.Max.X - _actionBox.Min.X) + 1;
			int h = (int)(_actionBox.Max.Y - _actionBox.Min.Y) + 1;

			_cutMask = new bool[w, h];
			_noise = new int[w, h];

			for (int x = 0; x < w; x++)
				for (int y = 0; y < h; y++)
					_noise[x, y] = _options.Rng.Next(0, Int32.MaxValue);

			int px = (int)(_options.Source.X - _actionBox.Min.X);
			int py = (int)(-_options.Source.Y - _actionBox.Min.Y);

			CarveCone(_cutMask, px, py, _options.Angle, _options.HalfAngle);
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			var box = new BoundingBox();
			box.Add(_status.Layer.ToPlane(_status.OriginalAct));

			var startX = (int)(box.Min.X - _actionBox.Min.X);
			var startY = (int)(box.Min.Y - _actionBox.Min.Y);

			var imgCopy = img.Clone();

			for (int x = 0; x < img.Width; x++) {
				for (int y = 0; y < img.Height; y++) {
					int xx = x + startX;
					int yy = y + startY;

					if (_cutMask[xx, yy] && !imgCopy.IsPixelTransparent(x, y)) {
						bool left = xx - 1 < 0 || _cutMask[xx - 1, yy];
						bool right = xx + 1 >= _cutMask.GetLength(0) || _cutMask[xx + 1, yy];
						bool top = yy - 1 < 0 || _cutMask[xx, yy - 1];
						bool bottom = yy + 1 >= _cutMask.GetLength(1) || _cutMask[xx, yy + 1];

						if (left && right && top && bottom) {
							img.SetPixelTransparent(x, y);
						}
						else {
							img.Pixels[y * img.Width + x] = _borderColorsIndexes[_options.Rng.Next(_borderColorsIndexes.Count)];
						}
					}
				}
			}
		}

		public void CarveCone(bool[,] mask, int px, int py, float angleDegrees, float halfAngleDegreees) {
			int w = mask.GetLength(0);
			int h = mask.GetLength(1);

			var angle = MathHelper.DegreesToRadians(angleDegrees);
			var halfAngle = MathHelper.DegreesToRadians(halfAngleDegreees);

			var leftAngle = angle - halfAngle;
			var rightAngle = angle + halfAngle;

			TkVector2 leftNormal = new TkVector2((float)Math.Sin(leftAngle), -(float)Math.Cos(leftAngle));
			TkVector2 rightNormal = new TkVector2(-(float)Math.Sin(rightAngle), (float)Math.Cos(rightAngle));

			for (int y = 0; y < h; y++) {
				float dy = y - py;

				for (int x = 0; x < w; x++) {
					if (mask[x, y])
						continue;

					float dx = x - px;

					bool insideLeft = (dx * leftNormal.X + dy * leftNormal.Y) >= 0;
					bool insideRight = (dx * rightNormal.X + dy * rightNormal.Y) >= 0;

					if (insideLeft && insideRight)
						mask[x, y] = true;
				}
			}
		}

		public override string Group => "Effects/Idle";
		public override string InputGesture => "{Dialog.AnimationCutSprite}";
		public override string Image => "empty.png";

		#endregion
	}

	public class EffectBreathing : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public float ScaleX;
			public float ScaleY;
			public int Ease;
			public bool FixedBottom;
		}

		private EffectOptions _options = new EffectOptions();
		private Func<float, float> _easeMethod;

		public EffectBreathing() : base("Breathing effect") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("ScaleX", 0.9f, 0f, 3f);
			effect.AddProperty("ScaleY", 1.3f, 0f, 3f);
			effect.AddProperty("Ease", 10, -50, 50);

			_animationComponent.DefaultSaveData.SetAnimation(0);
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.ScaleX = effect.GetProperty<float>("ScaleX");
			_options.ScaleY = effect.GetProperty<float>("ScaleY");
			_options.Ease = effect.GetProperty<int>("Ease");

			_easeMethod = InterpolationAnimation.GetEaseMethod(_options.Ease);
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			float t = (float)step / animLength;

			if (t > 0.5f)
				t = (1 - t) * 2;
			else
				t *= 2;

			t = _easeMethod(t);

			layer.ScaleX += layer.ScaleX * t * (_options.ScaleX - 1);
			layer.ScaleY += layer.ScaleX * t * (_options.ScaleY - 1);

			layer.OffsetX = (int)(layer.OffsetX + layer.OffsetX * t * (_options.ScaleX - 1));
			layer.OffsetY = (int)(layer.OffsetY + layer.OffsetY * t * (_options.ScaleY - 1));
		}

		public override string Group => "Effects/Idle";
		public override string InputGesture => "{Dialog.AnimationBreathing}";
		public override string Image => "effect_breathing.png";

		#endregion
	}

	public class BleedingOutlineEffect : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public int MaxHeight;
			public int MinHeight;
			public int PauseCycle;
			public float DeadPixelRate;
			public GrfColor Color;
			public int RngSeed;
			public Random Rng;
		}

		private EffectOptions _options = new EffectOptions();
		private HashSet<ActIndex> _processedActIndexes;
		private int _paletteInsertIndex = -1;

		public BleedingOutlineEffect() : base("Ash/Bleeding outline") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("MinHeight", 0, 0, 10);
			effect.AddProperty("MaxHeight", 4, 1, 10);
			effect.AddProperty("PauseCycle", 3, 0, 10);
			effect.AddProperty("DeadPixelRate", 0.5f, 0f, 1f);
			effect.AddProperty("Color", new GrfColor(255, 240, 209, 220), null, null);
			effect.AddProperty("RngSeed", 1234, 0, 10000);

			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.MinHeight = effect.GetProperty<int>("MinHeight");
			_options.MaxHeight = effect.GetProperty<int>("MaxHeight");
			_options.PauseCycle = effect.GetProperty<int>("PauseCycle");
			_options.DeadPixelRate = effect.GetProperty<float>("DeadPixelRate");
			_options.Color = effect.GetProperty<GrfColor>("Color");
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));

			if (_options.MinHeight > _options.MaxHeight) {
				var t = _options.MinHeight;
				_options.MinHeight = _options.MaxHeight;
				_options.MaxHeight = t;
			}

			_paletteInsertIndex = -1;
		}

		public class Stripe {
			public int Height;
			public int StartHeight;
			public int Dir;
			public int EmitTime;
		}

		private List<Stripe> _stripes = new List<Stripe>();
		private BoundingBox _actionBox;

		public override void OnPreviewProcessAction(Act act, Action action, int aid) {
			_stripes = new List<Stripe>();
			_actionBox = ActImaging.Imaging.GenerateBoundingBox(act, aid);

			int w = (int)(_actionBox.Max.X - _actionBox.Min.X) + 1;
			var min = _options.MinHeight;
			var max = _options.MaxHeight;

			if (min > max) {
				var t = max;
				max = min;
				min = t;
			}

			for (int i = 0; i < w; i++) {
				var stripe = new Stripe();
				int maxAttempt = 3;

				do {
					stripe.Height = _options.Rng.Next(min, max);
				} while (i > 0 && _stripes[i - 1].Height == stripe.Height && --maxAttempt > 0);

				stripe.Dir = (_options.Rng.Next() % 2) * 2 - 1;
				_stripes.Add(stripe);
			}

			// Create empty blocks
			int emptyCount = (int)(_options.DeadPixelRate * w);

			do {
				int x = _options.Rng.Next(0, w - 1);

				for (int i = 0; i < 5; i++) {
					int xx = (x + i) % w;

					if (_stripes[xx].Dir != 0) {
						_stripes[xx].Height = min;
						_stripes[xx].Dir = 0;
						emptyCount--;
					}

					if (emptyCount <= 0)
						break;
				}
			} while (emptyCount > 0);
		}

		public override void OnPreviewProcessFrame(int step, int animLength) {
			for (int x = 0; x < _stripes.Count; x++) {
				var stripe = _stripes[x];

				if (stripe.Dir == 0)
					continue;

				if (stripe.Height >= _options.MaxHeight)
					stripe.Dir = -1;
				else if (stripe.Height <= _options.MinHeight)
					stripe.Dir = 1;

				stripe.Height += stripe.Dir;
			}
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			var box = new BoundingBox();
			box.Add(_status.Layer.ToPlane(_status.OriginalAct));

			var start = (int)(box.Min.X - _actionBox.Min.X);

			img.Margin(0, 50, 0, 50);

			if (_paletteInsertIndex < 0) {
				_paletteInsertIndex = _addPaletteColor(_options.Color);
			}

			for (int x = 0; x < img.Width; x++) {
				bool found = false;

				for (int y = 0; y < img.Height; y++) {
					// Find edge of the sprite
					if (!found && !img.IsPixelTransparent(x, y)) {
						var stripe = _stripes[x + start];

						for (int yy = 0; yy < Math.Max(0, stripe.Height); yy++) {
							if (img.GrfImageType == GrfImageType.Indexed8) {
								img.Pixels[(y - yy) * img.Width + x] = (byte)_paletteInsertIndex;
							}
							else {
								img.SetColor(x, y - yy, _options.Color);
							}
						}

						found = true;
					}
					else if (found && img.IsPixelTransparent(x, y)) {
						found = false;
					}
				}
			}
		}

		public override void PostProcessLayer(Act act, Layer layer, int step, int animLength) {
			base.PostProcessLayer(act, layer, step, animLength);
			_processedActIndexes.Add(_status.ActIndex);
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

		public override string Group => "Effects/Idle";
		public override string InputGesture => "{Dialog.AnimationAsh}";
		public override string Image => "empty.png";

		#endregion
	}

	public class FadeInOutColorOverlayEffect : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public GrfColor Color;
			public int Ease;
			public bool Multiply;
		}

		private EffectOptions _options = new EffectOptions();
		private Func<float, float> _easeMethod;
		private int _paletteInsertIndex = -1;

		public FadeInOutColorOverlayEffect() : base("Fade in/out color") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Color", new GrfColor(200, 255, 150, 0), null, null);
			effect.AddProperty("Ease", 0, -50, 50);
			effect.AddProperty("Multiply/Additive", true, false, true);

			_animationComponent.DefaultSaveData.SetAnimation(0, 1);
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Color = effect.GetProperty<GrfColor>("Color");
			_options.Ease = effect.GetProperty<int>("Ease");
			_options.Multiply = effect.GetProperty<bool>("Multiply/Additive");

			_easeMethod = InterpolationAnimation.GetEaseMethod(_options.Ease);
			_paletteInsertIndex = -1;
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			float t = (float)step / animLength;

			if (t > 0.5f)
				t = (1 - t) * 2;
			else
				t *= 2;

			t = _easeMethod(t);

			if (_options.Multiply) {
				var vColor = layer.Color.ToTkVector4();
				layer.Color = ((_options.Color.ToTkVector4() - vColor) * t + vColor).ToGrfColor();
			}
			else {
				var spriteIndex = GetWhiteImageSpriteIndex(act, layer);

				if (!spriteIndex.Valid)
					return;

				var nLayer = new Layer(layer);
				nLayer.SprSpriteIndex = spriteIndex;
				nLayer.Color = new GrfColor((byte)(_options.Color.A * t), _options.Color.R, _options.Color.G, _options.Color.B);
				_layersToInsert.Add((_status.Fid, _status.Lid + 1, nLayer));
			}
		}

		private SpriteIndex GetWhiteImageSpriteIndex(Act act, Layer layer) {
			if (!_transformedSprites.TryGetValue((layer.SprSpriteIndex, 0), out SpriteIndex newSpriteIndex)) {
				var image = layer.GetImage(act.Sprite);

				if (image == null)
					return SpriteIndex.Null;

				image = image.Copy();

				if (image.GrfImageType == GrfImageType.Bgra32 || _generateBgra32Images) {
					if (_generateBgra32Images && image.GrfImageType == GrfImageType.Indexed8) {
						image.Convert(GrfImageType.Bgra32);
					}

					for (int i = 0; i < image.Pixels.Length; i += 4) {
						image.Pixels[i + 0] = 255;
						image.Pixels[i + 1] = 255;
						image.Pixels[i + 2] = 255;
						image.Pixels[i + 3] = (byte)(_options.Color.A * image.Pixels[i + 3] / 255);
					}
				}
				else {
					if (_paletteInsertIndex < 0) {
						var colors = _actInput.Sprite.Palette.Colors.ToList();

						for (int i = 1; i < colors.Count; i++) {
							if (_options.Color.Equals(colors[i])) {
								_paletteInsertIndex = i;
								break;
							}
						}

						if (_paletteInsertIndex == -1) {
							var unused = _actInput.Sprite.GetUnusedPaletteIndexes();

							if (unused.Count == 0) {
								_generateBgra32Images = true;
								return GetWhiteImageSpriteIndex(act, layer);
							}

							_paletteInsertIndex = unused.First();
							act.Sprite.Palette.SetColor(_paletteInsertIndex, GrfColor.White);
						}
					}

					image.SetPalette(act.Sprite.Palette.BytePalette);

					for (int i = 0; i < image.Pixels.Length; i++) {
						if (image.Pixels[i] != 0)
							image.Pixels[i] = (byte)_paletteInsertIndex;
					}
				}

				newSpriteIndex = act.Sprite.InsertAny(image);
				_transformedSprites[(layer.SprSpriteIndex, 0)] = newSpriteIndex;
			}

			return newSpriteIndex;
		}

		public override string Group => "Effects/Idle";
		public override string InputGesture => "{Dialog.AnimationFadeInOutColorOverlay}";
		public override string Image => "empty.png";

		#endregion
	}

	public class ImageProcessingEffect : IActScript {
		private string _displayName;
		protected string _inputGesture;
		protected Act _actInput;
		protected bool _generateBgra32Images;
		protected AnimationEditComponent _animationComponent;

		public virtual object DisplayName => _displayName;
		public virtual string Group => "Effects";
		public virtual string InputGesture => _inputGesture;
		public virtual string Image => null;

		public ImageProcessingEffect(string displayName) {
			_displayName = displayName;
		}

		public struct ProcessingStatus {
			public int Aid;
			public int Fid;
			public int Lid;
			public Action Action;
			public Frame Frame;
			public Layer Layer;
			public ActIndex ActIndex;
			public Act OriginalAct;
			public Act ModifiedAct;
		}

		public GrfColor TargetColor;
		protected Dictionary<(SpriteIndex, int), SpriteIndex> _transformedSprites;
		protected HashSet<int> _applyLayerIndexes;
		protected HashSet<byte> _temporaryUsedPaletteColors = new HashSet<byte>();
		protected bool _loopMissingFrames;
		protected bool _addEmptyFrame;
		protected int _animLength;
		protected int _animStart;
		protected List<(int FrameIndex, int LayerIndex, Layer Layer)> _layersToInsert = new List<(int FrameIndex, int LayerIndex, Layer Layer)>();
		protected ProcessingStatus _status;

		public void ApplyMask(GrfImage img, bool[,] mask) {
			int w = img.Width;
			int h = img.Height;

			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					if (mask[x, y]) {
						if (img.GrfImageType == GrfImageType.Indexed8)
							img.Pixels[w * y + x] = 0;
						else
							img.Pixels[(w * y + x) * 4 + 3] = 0;
					}
				}
			}
		}

		public virtual HashSet<int> GetApplyLayerIndexes(EffectConfiguration effect) {
			return _animationComponent.SaveData.Layers;
		}

		public virtual HashSet<int> GetAnimations(EffectConfiguration effect) {
			return _animationComponent.SaveData.Animations;
		}

		public bool IsLayerForProcess(int layerIndex) {
			if (_applyLayerIndexes == null || _applyLayerIndexes.Count == 0)
				return layerIndex == 0;

			return _applyLayerIndexes.Contains(layerIndex);
		}

		public virtual void OnAddProperties(EffectConfiguration effect) {
			_animationComponent = new AnimationEditComponent(_status.OriginalAct);
			_animationComponent.DefaultSaveData.AddEmptyFrame = true;
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.SetLayers(0);
			_animationComponent.DefaultSaveData.SetAnimation(4);
			effect.AddProperty("AnimationData", _animationComponent, null, null);
			_animationComponent.SetEffectProperty(effect.Properties["AnimationData"]);
			_animationComponent.LoadProperty();
		}

		public virtual void OnPostAddProperties(EffectConfiguration effect) {
			effect.SetToolTip("ApplyLayers", "What layers does this animation apply to. For example \"0-2;4;5\" would apply to layers 0, 1, 2, 4 and 5. Use \"0-99\" to apply to all layers.");
			effect.SetToolTip("AnimLength", "The length of the animation. If set to 0, the length of the animation is set for all the frames in the action.");
			effect.SetToolTip("AnimStart", "Delays the start of the animation. If set to 2, the animation will start at frame 2 instead of 0.");
			effect.SetToolTip("LoopMissingFrames", "When you use AnimLength and AnimLength is greater than the amount of frames in the current action, this option decides whether to create new frames by looping back from the start, or re-using the last frame on repeat.");
			effect.SetToolTip("AddEmptyFrame", "If set to true, an empty frame will be added to the animation after the AnimLength value.");
			effect.SetToolTip("Animation", "Tells which action indexes to apply the animation. For example \"2;3;4\", would apply to all actions for animations 2, 3 and 4. So 'Attack', 'Hit' and 'Dead'.");
			effect.SetToolTip("TargetX", "The X coordinate of the target effect. This value is in percentage, so 0 is the left most side while 1 is the right most side.");
			effect.SetToolTip("TargetY", "The Y coordinate of the target effect. This value is in percentage, so 0 is the top most side while 1 is the bottom most side.");
			effect.SetToolTip("End color", "The color of the layer(s) at the end of the animation. The layer(s) will slowly turn in into this value.");
		}

		public virtual void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			_status.OriginalAct = act;
			var effect = new EffectConfiguration(_displayName);
			OnAddProperties(effect);
			OnPostAddProperties(effect);
			effect.InvalidateSprite = true;

			SetupCorrectAnimation(effect, selectedActionIndex);

			effect.Apply(actEffect => {
				_applyLayerIndexes = GetApplyLayerIndexes(effect);
				_actInput = actEffect;
				_status.ModifiedAct = _actInput;
				_temporaryUsedPaletteColors.Clear();

				OnPreviewApplyEffect(effect);

				HashSet<int> animIndexes = GetAnimations(effect);

				// Copy effect from actEffect
				OnBackupCommand(effect);
				_transformedSprites = new Dictionary<(SpriteIndex, int), SpriteIndex>();

				actEffect.AllActions((action, aid) => {
					if (!ValidAnimation(aid, animIndexes))
						return;

					var animLength = _animLength <= 0 ? action.NumberOfFrames - _animStart : _animLength;

					if (animLength <= 0) {
						animLength = action.NumberOfFrames;
					}

					EnsureFrameCount(action, _animStart, animLength, _loopMissingFrames);
					OnPreviewProcessAction(actEffect, action, aid);

					_status.Action = action;
					_status.Aid = aid;

					// Apply animation
					ProcessAction(actEffect, action, _animStart, animLength);

					if (_addEmptyFrame) {
						if (action.Frames.Count == _animStart + animLength) {
							action.Frames.Add(new Frame());
						}
					}
				});

				OnPostBackupCommand();
			});
			effect.Display(act, selectedActionIndex);
		}

		public virtual void OnBackupCommand(EffectConfiguration effect) {
			
		}

		public virtual void OnPostBackupCommand() {

		}

		public virtual void OnPreviewProcessAction(Act act, Action action, int aid) {
			
		}

		public virtual void OnPreviewApplyEffect(EffectConfiguration effect) {
			_loopMissingFrames = _animationComponent.SaveData.LoopFrames;
			_addEmptyFrame = _animationComponent.SaveData.AddEmptyFrame;
			_animLength = _animationComponent.SaveData.AnimLength;
			_animStart = _animationComponent.SaveData.AnimStart;
		}

		private void SetupCorrectAnimation(EffectConfiguration effect, int selectedActionIndex) {
			// Pre-parse animation index field
			var animIndexes = GetAnimations(effect);

			if (!animIndexes.Contains(selectedActionIndex / 8)) {
				effect.PreferredSelectedAction = animIndexes.Min() * 8;
			}
		}

		private bool ValidAnimation(int aid, HashSet<int> animIndexes) {
			//bool mirror = (aid % 8) > 4;
			int animIndex = aid / 8;

			if (!animIndexes.Contains(animIndex))
				return false;

			return true;
		}

		public virtual void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			var sprIndex = layer.SprSpriteIndex;

			if (sprIndex.Valid) {
				SpriteIndex newSpriteIndex;

				if (!_transformedSprites.TryGetValue((sprIndex, step), out newSpriteIndex)) {
					var image = act.Sprite.GetImage(sprIndex).Copy();

					if (_generateBgra32Images)
						image.Convert(GrfImageType.Bgra32);

					newSpriteIndex = act.Sprite.InsertAny(image);
					ProcessImage(image, step, animLength);
					_transformedSprites[(sprIndex, step)] = newSpriteIndex;
				}

				layer.SprSpriteIndex = newSpriteIndex;

				PostProcessLayer(act, layer, step, animLength);
			}
		}

		public virtual void PostProcessLayer(Act act, Layer layer, int step, int animLength) {
			
		}

		public virtual void ProcessImage(GrfImage img, int step, int totalSteps) {

		}

		public void ProcessColor(Layer layer, float mult, GrfColor targetColor) {
			layer.Color = layer.Color * (1 - mult) + targetColor * mult;
		}

		public virtual void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animLength = animLength <= 0 ? action.Frames.Count : animLength;
			_layersToInsert.Clear();

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];

				_status.Frame = frame;
				_status.Fid = i;

				int step = i - animStart;
				
				OnPreviewProcessFrame(step, animLength);
				OnProcessFrame(step, animLength);
				OnPostProcessFrame(step, animLength);
			}

			foreach (var toInsert in _layersToInsert.GroupBy(p => p.FrameIndex)) {
				var frame = action[toInsert.Key];

				foreach (var insert in toInsert.OrderByDescending(p => p.LayerIndex)) {
					frame.Layers.Insert(insert.LayerIndex, insert.Layer);
				}
			}
		}

		public virtual void OnPreviewProcessFrame(int step, int animLength) {
		}

		public virtual void OnPostProcessFrame(int step, int animLength) {
		}

		public virtual void OnProcessFrame(int step, int animLength) {
			var frame = _status.Frame;
			float mult = (float)step / (animLength - 1);

			for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
				if (!IsLayerForProcess(layerIndex))
					continue;

				var layer = frame[layerIndex];

				_status.Layer = layer;
				_status.Lid = layerIndex;

				if (TargetColor != null)
					ProcessColor(layer, mult, TargetColor);

				_status.ActIndex = new ActIndex() { ActionIndex = _status.Aid, FrameIndex = _status.Fid, LayerIndex = _status.Lid };
				ProcessLayer(_status.ModifiedAct, layer, step, animLength);
			}
		}

		public virtual void EnsureFrameCount(Action action, int animStart, int animLength, bool loopMissingFrames) {
			if (animLength == 0)
				return;

			// Fix frame count
			if (action.Frames.Count < animStart + animLength) {
				int bIdx = 0;
				int startFrameCount = action.Frames.Count;

				for (int i = action.Frames.Count; i < animStart + animLength; i++) {
					if (loopMissingFrames) {
						action.Frames.Add(new Frame(action.Frames[bIdx]));
					}
					else {
						action.Frames.Add(new Frame(action.Frames[startFrameCount - 1]));
					}

					bIdx++;
					bIdx = bIdx % startFrameCount;
				}
			}
		}

		protected int _addPaletteColor(GrfColor color) {
			if (_actInput.Sprite.Palette == null)
				return -1;

			var colors = _actInput.Sprite.Palette.Colors.ToList();

			for (int i = 1; i < colors.Count; i++) {
				if (color.Equals(colors[i])) {
					return i;
				}
			}

			var unused = _actInput.Sprite.GetUnusedPaletteIndexes();

			foreach (var used in _temporaryUsedPaletteColors) {
				unused.Remove(used);
			}

			if (unused.Count == 0) {
				_generateBgra32Images = true;
				return -1;
			}

			var index = unused.First();
			_temporaryUsedPaletteColors.Add(index);
			var c = new GrfColor(color);
			c.A = 255;
			_actInput.Sprite.Palette.SetColor(index, c);
			return index;
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
