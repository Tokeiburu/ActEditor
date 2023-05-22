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
			get { return "Add frames from X to Y"; }
		}
	   
		public string Group {
			get { return "Scripts"; }
		}
	   
		public string InputGesture {
			get { return "{Scripts.AddFramesFromXtoY}"; }
		}
	   
		public string Image {
			get { return "addgrf.png"; }
		}
	   
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			var effect = new EffectConfiguration("AddFrames");
			effect.AddProperty("SpriteFrom", 0, 0, act.Sprite.NumberOfImagesLoaded);
			effect.AddProperty("SpriteTo", 5, 0, act.Sprite.NumberOfImagesLoaded);
			effect.AddProperty("OffsetX", 0, -100, 100);
			effect.AddProperty("OffsetY", 0, -100, 100);
			effect.AddProperty("Color", new GrfColor(255, 255, 255, 255), null, null);
			
			effect.Apply(actInput => {
				int offsetX = effect.GetProperty<int>("OffsetX");
				int offsetY = effect.GetProperty<int>("OffsetY");
				GrfColor color = effect.GetProperty<GrfColor>("Color");
				int spriteFrom = effect.GetProperty<int>("SpriteFrom");
				int spriteTo = effect.GetProperty<int>("SpriteTo");
				
				if (spriteTo < spriteFrom)
					return;
				
				int spriteCount = spriteTo - spriteFrom + 1;
				
				actInput.Commands.Backup(_ => {
					for (int i = 0; i < spriteCount; i++) {
						Frame f = new Frame();
						Layer l = new Layer(spriteFrom + i, actInput.Sprite);
						
						l.OffsetX = offsetX;
						l.OffsetY = offsetY;
						l.Color = color;
						f.Layers.Add(l);
						
						actInput[selectedActionIndex].Frames.Add(f);
					}
				}, "Add frames");
			});
			effect.Display(act, selectedActionIndex);
		}
	   
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}