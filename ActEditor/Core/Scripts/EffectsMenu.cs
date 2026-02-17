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
	public class EffectFadeAnimation : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			EffectConfiguration effect = new EffectConfiguration("EffectFadeAnimation");
			effect.AddProperty("AnimLength", 7, 3, 30);
			effect.Apply(actInput => {
				int ival = effect.GetProperty<int>("AnimLength");

				Action action = actInput[selectedActionIndex];

				// Ensures there are enough frames
				while (action.NumberOfFrames - 1 < ival + selectedFrameIndex) {
					action.Frames.Add(new Frame(action.Frames.Last()));
				}

				double[] alphaValues = new double[ival + 1];

				for (int i = 0; i <= ival; i++) {
					alphaValues[i] = 1d - (double)i / ival;
				}

				for (int i = 1; i <= ival; i++) {
					Frame frame = action.Frames[selectedFrameIndex + i];
						
					if (i == ival) {
						frame.Layers.Clear();
					}
					else {
						foreach (Layer layer in frame.Layers) {
							layer.Color = new GrfColor((byte)(alphaValues[i] * layer.Color.A), layer.Color.R, layer.Color.G, layer.Color.B);
						}
					}
				}
			});
			effect.ActIndexSelectorReadonly = true;
			effect.Display(act, selectedActionIndex);
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName => "Generate fade animation";
		public string Group => "Effects";
		public string InputGesture => "{Dialog.AnimationFade|Ctrl-Alt-T}";
		public string Image => "fade.png";

		#endregion
	}

	public class EffectBreathing : ImageProcessingEffect {
		#region IActScript Members

		public class EffectOptions {
			public float ScaleX;
			public float ScaleY;
		}

		private EffectOptions _options = new EffectOptions();
		private Func<float, float> _easeMethod;

		public EffectBreathing() : base("Breathing effect") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("ScaleX", 1f, 0f, 3f);
			effect.AddProperty("ScaleY", 1.3f, 0f, 3f);

			_animationComponent.DefaultSaveData.SetAnimation(0);
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.ScaleX = effect.GetProperty<float>("ScaleX");
			_options.ScaleY = effect.GetProperty<float>("ScaleY");
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			float t = (float)step / animLength;

			if (t > 0.5f) {
				Z.F();
			}

			layer.ScaleX *= (float)(1 + Math.Sin(t) * 0.015) * _options.ScaleX;
			layer.ScaleY *= (float)(1 - Math.Sin(t) * 0.020) * _options.ScaleY;
			layer.OffsetY += (int)(Math.Sin(t) * 0.6 * _options.ScaleY);
		}

		public override string Group => "Effects/Idle";
		public override string InputGesture => "{Dialog.AnimationBreathing}";
		public override string Image => "effect_breathing.png";

		#endregion
	}

	//public class EffectBreathing : IActScript {
	//	#region IActScript Members
	//
	//	public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
	//		if (act == null) return;
	//
	//		EffectConfiguration effect = new EffectConfiguration("EffectBreathing");
	//		effect.AddProperty("Scalar", 0.04F, 0, 0.2F);
	//		effect.AddProperty("AnimLength", 20, 3, 100);
	//		effect.AddProperty("Animation", "0;1;2;3;4", "", "");
	//		effect.Apply(actInput => {
	//			float b = effect.GetProperty<float>("Scalar");
	//			int animLength = effect.GetProperty<int>("AnimLength");
	//			string animation = effect.GetProperty<string>("Animation");
	//			int midPoint = animLength / 2;
	//			List<float> scales = new List<float>();
	//			List<float> scalesR;
	//
	//			for (int i = 0; i <= midPoint; i++) {
	//				scales.Add(1 + (b * i));
	//			}
	//
	//			scalesR = new List<float>(scales);
	//			scalesR.Reverse();
	//			scalesR = scalesR.Skip(1).ToList();
	//
	//			scales.AddRange(scalesR);
	//
	//			// Only process the animation indexes provided by the animation variable; QueryIndexProvider provides index for the format such as 1-5;7;8
	//			var animIndexes = new HashSet<int>(new QueryIndexProvider(animation).GetIndexes());
	//
	//			// Copy effect from actEffect
	//			actInput.AllActions((action, aid) => {
	//				int animIndex = aid / 8;
	//
	//				if (!animIndexes.Contains(animIndex))
	//					return;
	//
	//				var insertLocation = Math.Min(selectedFrameIndex, action.NumberOfFrames);
	//
	//				for (int i = 0; i < animLength; i++) {
	//					var frameCopy = new Frame(action[insertLocation + i]);
	//					action.Frames.Add(frameCopy);
	//					action[insertLocation + i].Scale(1f, scales[i]);
	//				}
	//			});
	//		});
	//		effect.Display(act, selectedActionIndex);
	//	}
	//
	//	public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
	//		return act != null && !EffectConfiguration.Displayed;
	//	}
	//
	//	public object DisplayName => "Breathing [Palooza]";
	//	public string Group => "Effects/Idle";
	//	public string InputGesture => "{Dialog.AnimationBreathing}";
	//	public string Image => "effect_breathing.png";
	//
	//	#endregion
	//}

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
					ProcessLayer(act, layer, step, animLength);
				}
			}

			foreach (var toInsert in _layersToInsert.GroupBy(p => p.FrameIndex)) {
				var frame = action[toInsert.Key];

				foreach (var insert in toInsert.OrderByDescending(p => p.LayerIndex)) {
					frame.Layers.Insert(insert.LayerIndex, insert.Layer);
				}
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

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
