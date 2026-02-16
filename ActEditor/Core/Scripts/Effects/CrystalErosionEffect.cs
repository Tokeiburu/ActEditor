using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using Action = GRF.FileFormats.ActFormat.Action;

namespace ActEditor.Core.Scripts.Effects {
	public class CrystalErosionEffect : ImageProcessingEffect {
		public override string Group => "Effects/Dead";

		public class EffectOptions {
			public float TargetX;
			public float TargetY;
			public int CrystalCount;
			public bool Implode;
			public bool UseOutline;
			public GrfColor OutlineColor;
		}

		private EffectOptions _options = new EffectOptions();
		private List<CrystalCell> _cells;
		private HashSet<ActIndex> _processedActIndexes;

		public CrystalErosionEffect() : base("Crystal erosion [Tokei]") {
			_inputGesture = "{Dialog.AnimationCrystalErosion}";
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Animation", "4", "", "");
			effect.AddProperty("TargetX", 0.5f, 0.0f, 1.0f);
			effect.AddProperty("TargetY", 0.7f, 0.0f, 1.0f);
			effect.AddProperty("End color", new GrfColor(255, 0, 0, 0), null, null);

			effect.AddProperty("CrystalCount", 40, 1, 200);
			effect.AddProperty("Implode", true, false, true);
			effect.AddProperty("UseOutline", false, false, true);
			effect.AddProperty("OutlineColor", new GrfColor(100, 255, 255, 255), null, null);
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.TargetX = effect.GetProperty<float>("TargetX");
			_options.TargetY = effect.GetProperty<float>("TargetY");

			_options.CrystalCount = effect.GetProperty<int>("CrystalCount");
			_options.Implode = effect.GetProperty<bool>("Implode");
			_options.UseOutline = effect.GetProperty<bool>("UseOutline");
			_options.OutlineColor = effect.GetProperty<GrfColor>("OutlineColor");

			TargetColor = effect.GetProperty<GrfColor>("End color");
		}

		public override void OnPreviewProcessAction(Act act, Action action, int aid) {
			base.OnPreviewProcessAction(act, action, aid);

			int maxWidth = 0;
			int maxHeight = 0;
			GrfImage imageInput = null;

			action.AllLayers(layer => {
				var image = act.Sprite.GetImage(layer);

				if (imageInput == null)
					imageInput = image;

				if (image != null) {
					maxWidth = Math.Max(maxWidth, image.Width);
					maxHeight = Math.Max(maxHeight, image.Height);
				}
			});

			int targetX = (int)(_options.TargetX * maxWidth);
			int targetY = (int)(_options.TargetY * maxHeight);

			_cells = BuildCrystals(maxWidth, maxHeight, targetX, targetY, _options.CrystalCount, 1234);

			_cells.Sort((a, b) => b.DistanceToTarget.CompareTo(a.DistanceToTarget));

			if (!_options.Implode)
				_cells.Reverse();
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			int cellsPerStep = (int)Math.Ceiling((float)_cells.Count / totalSteps);

			int killUpTo = Math.Min(_cells.Count, step * cellsPerStep);

			for (int i = 0; i < killUpTo; i++) {
				foreach (var (x, y) in _cells[i].Pixels) {
					img.SetPixelTransparent(x, y);
				}
			}
		}

		public override void ProcessAction(Act act, Action action, int animStart, int animLength) {
			animLength = animLength <= 0 ? action.Frames.Count : animLength;

			for (int i = animStart; i < animStart + animLength; i++) {
				Frame frame = action[i];
				int step = i - animStart;
				float mult = (float)step / (animLength - 1);

				List<(int Index, Layer Layer)> toInsert = new List<(int, Layer)>();

				for (int layerIndex = 0; layerIndex < frame.Layers.Count; layerIndex++) {
					if (!IsLayerForProcess(layerIndex))
						continue;

					var layer = frame[layerIndex];
					_processedActIndexes.Add(new ActIndex { ActionIndex = _status.Aid, FrameIndex = i, LayerIndex = layerIndex });

					if (TargetColor != null)
						ProcessColor(layer, mult, TargetColor);

					ProcessLayer(act, layer, step, animLength);

					if (_options.UseOutline) {
						// Create outline
						var sprIndex = layer.SprSpriteIndex;

						if (sprIndex.Valid) {
							SpriteIndex newSpriteIndex;
							var imgBack = act.Sprite.GetImage(sprIndex);

							if (imgBack.GrfImageType == GrfImageType.Indexed8) {
								if (!_transformedSprites.TryGetValue((sprIndex, 10000 + step), out newSpriteIndex)) {
									newSpriteIndex = act.Sprite.InsertAny(new GrfImage(new byte[imgBack.Width * imgBack.Height * 4], imgBack.Width, imgBack.Height, GrfImageType.Bgra32));
									GrfImage image = act.Sprite.GetImage(newSpriteIndex);

									_transformedSprites[(sprIndex, 10000 + step)] = newSpriteIndex;
									GrfColor color = _options.OutlineColor;

									for (int x = 0; x < image.Width - 1; x++) {
										for (int y = 0; y < image.Height - 1; y++) {
											// From top left to bottom right
											if (
												//(imgBack.IsPixelTransparent(x, y) && (!imgBack.IsPixelTransparent(x + 1, y) || !imgBack.IsPixelTransparent(x, y + 1)))
												//|| (!imgBack.IsPixelTransparent(x, y) && (imgBack.IsPixelTransparent(x + 1, y) || imgBack.IsPixelTransparent(x, y + 1)))
												//|| 
												(!imgBack.IsPixelTransparent(x, y) && ((_ids[x + 1, y] != _ids[x, y]) || (_ids[x, y + 1] != _ids[x, y])))
												) {
												image.Pixels[4 * (y * image.Width + x) + 0] = color.B;
												image.Pixels[4 * (y * image.Width + x) + 1] = color.G;
												image.Pixels[4 * (y * image.Width + x) + 2] = color.R;
												image.Pixels[4 * (y * image.Width + x) + 3] = color.A;
											}
										}
									}
								}

								var outline = new Layer(layer);
								outline.Color = GrfColor.White;
								outline.SprSpriteIndex = newSpriteIndex;

								toInsert.Add((layerIndex + 1, outline));
							}
						}
					}
				}

				foreach (var insert in toInsert.OrderByDescending(p => p.Index)) {
					frame.Layers.Insert(insert.Index, insert.Layer);
				}
			}
		}

		public class CrystalCell {
			public List<(int x, int y)> Pixels = new List<(int x, int y)>();
			public float DistanceToTarget;
		}

		private int[,] _ids;

		public List<CrystalCell> BuildCrystals(int w, int h, int targetX, int targetY, int crystalCount, int seed) {
			var rnd = new Random(seed);
			_ids = new int[w, h];

			var seeds = new List<(int x, int y)>();
			while (seeds.Count < crystalCount) {
				int x = rnd.Next(w);
				int y = rnd.Next(h);

				seeds.Add((x, y));
			}

			var cells = new CrystalCell[crystalCount];
			for (int i = 0; i < crystalCount; i++)
				cells[i] = new CrystalCell();

			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					int best = 0;
					int bestDist = int.MaxValue;

					for (int i = 0; i < seeds.Count; i++) {
						int dx = x - seeds[i].x;
						int dy = y - seeds[i].y;
						int d = dx * dx + dy * dy;

						if (d < bestDist) {
							bestDist = d;
							best = i;
						}
					}

					_ids[x, y] = best;
					cells[best].Pixels.Add((x, y));
				}
			}

			for (int i = 0; i < cells.Length; i++) {
				var s = seeds[i];
				int dx = s.x - targetX;
				int dy = s.y - targetY;
				cells[i].DistanceToTarget = (float)Math.Sqrt(dx * dx + dy * dy);
			}

			return cells.ToList();
		}

		public override void OnBackupCommand(EffectConfiguration effect) {
			_processedActIndexes = new HashSet<ActIndex>();
		}

		public override void OnPostBackupCommand() {
			// Cleanup images...
			ActHelper.TrimImages(_actInput, _processedActIndexes.ToList(), 0x10);

			for (int i = _actInput.Sprite.Images.Count - 1; i >= 0; i--) {
				var image = _actInput.Sprite.Images[i];

				if (image.Width == 1 && image.Height == 1 && image.IsPixelTransparent(0, 0)) {
					_actInput.Sprite.Remove(i, _actInput, EditOption.AdjustIndexes);
				}
			}
		}
	}
}
