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
using GRF.Image;
using GRF.Image.Decoders;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace Scripts {
	public class Script : IActScript {
		public object DisplayName {
			get { return "Generate sprite from selection"; }
		}
		
		public string Group {
			get { return "Scripts"; }
		}
		
		public string InputGesture {
			get { return "Ctrl-Shift-K"; }
		}
		
		public string Image {
			get { return "arrowdown.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			var frame = act[selectedActionIndex, selectedFrameIndex];
			
			if (selectedLayerIndexes.Length == 0) {
				selectedLayerIndexes = new int[frame.NumberOfLayers];

				for (int i = 0; i < selectedLayerIndexes.Length; i++) {
					selectedLayerIndexes[i] = i;
				}
			}

			if (selectedLayerIndexes.Length == 0) {
				ErrorHandler.HandleException("No layers found.", ErrorLevel.Warning);
			}
			
			int absoluteIndex = -1;

			List<Layer> layers = act[selectedActionIndex, selectedFrameIndex].Layers;
			List<Layer> selected = selectedLayerIndexes.Select(index => layers[index]).ToList();
			
			try {
				act.Commands.ActEditBegin("Generate single sprite");

				Act action = new Act(act.Sprite);
				action.AddAction();
				action.Commands.FrameInsertAt(0, 0);
				action.Commands.LayerAdd(0, 0, selected.ToArray());

				BitmapFrame bitFrame;

				ImageSource image = ActImaging.Imaging.GenerateFrameImage(action, action[0, 0]);
				bitFrame = ActImaging.Imaging.ForceRender(image, BitmapScalingMode.NearestNeighbor);

				PngBitmapEncoder encoder = new PngBitmapEncoder();
				encoder.Frames.Add(bitFrame);

				GrfImage grfImage;

				using (MemoryStream stream = new MemoryStream()) {
					encoder.Save(stream);

					stream.Seek(0, SeekOrigin.Begin);
					byte[] imData = new byte[stream.Length];
					stream.Read(imData, 0, imData.Length); 

					grfImage = new GrfImage(imData);

					if (selected.All(p => p.IsIndexed8())) {
						grfImage.Convert(new Indexed8FormatConverter {ExistingPalette = act.Sprite.Palette.BytePalette, Options = Indexed8FormatConverter.PaletteOptions.UseExistingPalette}, null);
					}
					else {
						grfImage.Convert(new Bgra32FormatConverter(), null);
					}
				}

				absoluteIndex = grfImage.GrfImageType == GrfImageType.Indexed8 ? act.Sprite.NumberOfIndexed8Images : act.Sprite.NumberOfImagesLoaded;
				var sprIndex = act.Sprite.InsertAny(grfImage);
				
				GRF.Graphics.BoundingBox box = ActImaging.Imaging.GenerateFrameBoundingBox(action, frame);
				
				int offsetX = (int) ((int) ((box.Max.X - box.Min.X + 1) / 2) + box.Min.X);
				int offsetY = (int) ((int) ((box.Max.Y - box.Min.Y + 1) / 2) + box.Min.Y);
				var layer = new Layer(sprIndex);
				
				layer.OffsetX = offsetX;
				layer.OffsetY= offsetY;
				
				frame.Layers.Add(layer);
			}
			catch (Exception err) {
				act.Commands.ActCancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
				return;
			}
			finally {
				act.Commands.ActEditEnd();
			}
		}
		
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}
