using System;
using System.Collections.Generic;
using System.Linq;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using Utilities.IndexProviders;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts {
	public class EffectFadeAnimation : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			EffectConfiguration effect = new EffectConfiguration("EffectFadeAnimation");
			effect.AddProperty("AnimLength", 7, 3, 30);
			effect.Apply(actInput => {
				int ival = effect.GetProperty<int>("AnimLength");

				actInput.Commands.Backup(_ => {
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
				}, "Generate fade animation");
			});
			effect.ActIndexSelectorReadonly = true;
			effect.Display(act, selectedActionIndex);
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Generate fade animation"; }
		}

		public string Group {
			get { return "Effects"; }
		}

		public string InputGesture {
			get { return "{Dialog.AnimationFade|Ctrl-Alt-T}"; }
		}

		public string Image {
			get { return "fade.png"; }
		}

		#endregion
	}

	public class EffectReceivingHit : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			try {
				act.Commands.Begin();
				act.Commands.Backup(actInput => {
					Action action = actInput[selectedActionIndex];

					int baseAction = selectedActionIndex % 8;

					if (action.NumberOfFrames == 1) {
						Frame frame0 = new Frame(action.Frames[0]);

						TkVector2 point = new TkVector2(0, 0);

						if (baseAction <= 1) {
							point = new TkVector2(4, -9);
						}
						else if (baseAction <= 3) {
							point = new TkVector2(4, -4);
						}
						else if (baseAction <= 5) {
							point = new TkVector2(-4, -4);
						}
						else if (baseAction <= 7) {
							point = new TkVector2(-4, -9);
						}

						frame0.Translate((int)point.X, (int)point.Y);

						action.Frames.Add(new Frame(frame0));
						action.Frames.Add(new Frame(frame0));
						action.Frames.Add(new Frame(frame0));
						action.Frames.Add(new Frame(frame0));
					}

					Frame frameLast = action.Frames.Last();

					while (action.NumberOfFrames < 5) {
						action.Frames.Add(new Frame(frameLast));
					}

					float[] scales = { 1f, 1.15f, 1.09f, 1.03f, 1f };

					for (int i = 1; i < 5; i++) {
						List<Layer> layers = action.Frames[i].Layers.Select(p => new Layer(p)).ToList();

						layers.ForEach(p => p.Scale(scales[i], scales[i]));
						layers.ForEach(p => p.Color = new GrfColor((byte)(0.58d * p.Color.A), p.Color.R, p.Color.G, p.Color.B));

						action.Frames[i].Layers.AddRange(layers);
					}
				}, "Generate receiving hit animation");
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Generate receiving damage animation"; }
		}

		public string Group {
			get { return "Effects"; }
		}

		public string InputGesture {
			get { return "{Dialog.AnimationReceivingHit}"; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}

	public class EffectStrokeSilouhette : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			EffectConfiguration effect = new EffectConfiguration("EffectStrokeSilouhette");
			effect.AddProperty("ScaleX", 0.082f, 0f, 1f);
			effect.AddProperty("ScaleY", 0.035f, 0f, 1f);
			effect.AddProperty("Color", new GrfColor("0xFFAD5500"), null, null);
			effect.AddProperty("Animation", "0;1;2;3;4;5;6;7;8;9;10", "", "");
			effect.Apply(actInput => {
				float scaleX = effect.GetProperty<float>("ScaleX");
				float scaleY = effect.GetProperty<float>("ScaleY");
				GrfColor color = effect.GetProperty<GrfColor>("Color");
				string animation = effect.GetProperty<string>("Animation");

				// Only process the animation indexes provided by the animation variable; QueryIndexProvider provides index for the format such as 1-5;7;8
				var animIndexes = new HashSet<int>(new QueryIndexProvider(animation).GetIndexes());

				// Copy effect from actEffect
				actInput.Commands.Backup(_ => {
					actInput.AllFrames((frame, aid, fid) => {
						int animIndex = aid / 8;

						if (!animIndexes.Contains(animIndex))
							return;

						if (frame.Layers.Count > 0) {
							var layer = new Layer(frame[0]);
							layer.ScaleX += scaleX;
							layer.ScaleY += scaleY;
							layer.SetColor(color);
							frame.Layers.Insert(0, layer);
						}
					});
				});
			});
			effect.Display(act, selectedActionIndex);
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		public object DisplayName {
			get { return "Stroke silouhette [Palooza]"; }
		}

		public string Group {
			get { return "Effects"; }
		}

		public string InputGesture {
			get { return "{Dialog.AnimationStrokeSilouhette}"; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}

	public class EffectBreathing : IActScript {
		#region IActScript Members

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			EffectConfiguration effect = new EffectConfiguration("EffectBreathing");
			effect.AddProperty("Scalar", 0.00025F, 0, 0.1F);
			effect.AddProperty("AnimLength", 20, 3, 100);
			effect.AddProperty("Animation", "0;1;2;3;4", "", "");
			effect.Apply(actInput => {
				float b = effect.GetProperty<float>("Scalar");
				int animLength = effect.GetProperty<int>("AnimLength");
				string animation = effect.GetProperty<string>("Animation");
				int midPoint = animLength / 2;
				List<float> scales = new List<float>();
				List<float> scalesR;

				for (int i = 0; i <= midPoint; i++) {
					scales.Add(1 + (b * i));
				}

				scalesR = new List<float>(scales);
				scalesR.Reverse();
				scalesR = scalesR.Skip(1).ToList();

				scales.AddRange(scalesR);

				// Only process the animation indexes provided by the animation variable; QueryIndexProvider provides index for the format such as 1-5;7;8
				var animIndexes = new HashSet<int>(new QueryIndexProvider(animation).GetIndexes());

				// Copy effect from actEffect
				actInput.Commands.Backup(_ => {
					actInput.AllActions((action, aid) => {
						int animIndex = aid / 8;

						if (!animIndexes.Contains(animIndex))
							return;

						var insertLocation = Math.Min(selectedFrameIndex, action.NumberOfFrames);

						for (int i = 0; i < animLength; i++) {
							var frameCopy = new Frame(action[insertLocation + i]);
							action.Frames.Add(frameCopy);
							action[insertLocation + i].Scale(1f, scales[i]);
						}
					});
				});
			});
			effect.Display(act, selectedActionIndex);
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null && !EffectConfiguration.Displayed;
		}

		public object DisplayName {
			get { return "Breathing [Palooza]"; }
		}

		public string Group {
			get { return "Effects"; }
		}

		public string InputGesture {
			get { return "{Dialog.AnimationBreathing}"; }
		}

		public string Image {
			get { return null; }
		}

		#endregion
	}
}
