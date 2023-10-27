using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GRF.Image.Decoders;
using GRF.Graphics;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Utilities.Extension;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;
using Point = System.Windows.Point;
 
namespace Scripts {
	public class Script : IActScript {
		public object DisplayName {
			get { return "Add sprite effect [Monkey/Tokeiburu]"; }
		}
	   
		public string Group {
			get { return "Scripts"; }
		}
	   
		public string InputGesture {
			get { return "{Scripts.AddSpriteEffect}"; }
		}
	   
		public string Image {
			get { return "spritemaker.png"; }
		}
	   
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			var effect = new EffectConfiguration("AddSpriteEffect");
			effect.AddProperty("OffsetX", 0, -100, 100);
			effect.AddProperty("OffsetY", 0, -100, 100);
			effect.AddProperty("Back/Front", 1, 0, 1);
			effect.AddProperty("Scale", 1f, 0f, 5f);
			effect.AddProperty("Color", new GrfColor(255, 255, 255, 255), null, null);
			effect.AddProperty("Animation", "0;1;2;3;4;5;6;7;8;9;10;11;12", "", "");
			effect.AddProperty("Delay", 0, 0, 10);
			effect.AddProperty("Effect", "script7_add_effect*.act", "FileSelect", null);
			
			effect.InvalidateSprite = true;
			effect.Apply(actInput => {
				int offsetX = effect.GetProperty<int>("OffsetX");
				int offsetY = effect.GetProperty<int>("OffsetY");
				int frontBack = effect.GetProperty<int>("Back/Front");
				float scale = effect.GetProperty<float>("Scale");
				GrfColor color = effect.GetProperty<GrfColor>("Color");
				string animation = effect.GetProperty<string>("Animation");
				int delay = effect.GetProperty<int>("Delay");
				string effectPath = effect.GetProperty<string>("Effect");
				var actEffectPath = effectPath.ReplaceExtension(".act");
				var sprEffectPath = effectPath.ReplaceExtension(".spr");
				var actEffect = new Act(actEffectPath, sprEffectPath);

				// Insert new sprite images (only bgra32 images allowed) and prevent duplicates
				List<int> redirects = new List<int>();

				for (int i = 0; i < actEffect.Sprite.NumberOfImagesLoaded; i++) {
					bool handled = false;

					for (int j = actInput.Sprite.NumberOfIndexed8Images; j < actInput.Sprite.NumberOfImagesLoaded; j++) {
						if (actInput.Sprite.Images[j].Equals(actEffect.Sprite.Images[i])) {
							redirects.Add(j - actInput.Sprite.NumberOfIndexed8Images);
							handled = true;
							break;
						}
					}

					if (!handled) {
						redirects.Add(actInput.Sprite.NumberOfBgra32Images);
						actInput.Commands.SpriteAdd(actEffect.Sprite.Images[i]);
					}
				}
				
				// Only process the animation indexes provided by the animation variable; QueryIndexProvider provides index for the format such as 1-5;7;8
				HashSet<int> animIndexes;
				
				try {
					animIndexes = new HashSet<int>(new Utilities.IndexProviders.QueryIndexProvider(animation).GetIndexes());
				}
				catch {
					return;
				}

				// Copy effect from actEffect
				actInput.Commands.Backup(_ => {
					actInput.AllFrames((frame, aid, fid) => {
						int eFid = (fid + delay) % actEffect[0].Frames.Count;
						bool mirror = (aid % 8) > 4;
						int animIndex = aid / 8;
						
						if (!animIndexes.Contains(animIndex))
							return;

						List<Layer> layers = new List<Layer>();
						
						foreach (var layer in actEffect[0, eFid].Layers) {
							var copyLayer = new Layer(layer);
							
							// Special handling for -1 sprite index. I use them to force a color on the previous sprites.
							if (copyLayer.SpriteIndex == -1) {
								foreach (var oriLayer in frame.Layers) {
									oriLayer.Color = copyLayer.Color;
								}
								
								continue;
							}
							
							copyLayer.SpriteIndex = redirects[copyLayer.SpriteIndex];
							copyLayer.OffsetX += offsetX;
							copyLayer.OffsetY += offsetY;
							copyLayer.Color = color;
							copyLayer.ScaleX *= scale;
							copyLayer.ScaleY *= scale;

							if (mirror) {
								copyLayer.OffsetX *= -1;
								copyLayer.Mirror = !copyLayer.Mirror;
							}
							
							layers.Add(copyLayer);
						}
						
						if (frontBack == 1) {
							frame.Layers.AddRange(layers);
						}
						else {
							frame.Layers.InsertRange(0, layers);
						}
					});
				}, "Add sprite effect");
			});
			effect.Display(act, selectedActionIndex);
		}
	   
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}