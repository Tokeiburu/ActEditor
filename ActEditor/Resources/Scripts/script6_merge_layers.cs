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
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;
using Point = System.Windows.Point;
 
namespace Scripts {
	public class Script : IActScript {
		public object DisplayName {
			get { return "Merge layers (new sprites)"; }
		}
	   
		public string Group {
			get { return "Scripts"; }
		}
	   
		public string InputGesture {
			get { return "{Scripts.MergeLayers}"; }
		}
	   
		public string Image {
			get { return "addgrf.png"; }
		}
	   
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
		   
			try {
				act.Commands.Begin();
				act.Commands.Backup(_ => {
					int count = act.GetAllFrames().Count + 1;
					int index = 0;
				   
					TaskManager.DisplayTaskC("Rendering frames...", "Please wait...", () => index, count, new Action<Func<bool>>(isCancelling => {
						try {
							foreach (var action in act) {
								foreach (var frame in action) {
									if (frame.Layers.Count <= 1) {
										index++;
										continue;
									}
									if (isCancelling()) return;
								   
									var image = frame.Render(act);
									var box = ActImaging.Imaging.GenerateFrameBoundingBox(act, frame);
									SpriteIndex sprIndex = SpriteIndex.Null;
								   
									for (int i = 0; i < act.Sprite.Images.Count; i++) {
										if (image.Equals(act.Sprite.Images[i])) {
											if (isCancelling()) return;
											sprIndex = SpriteIndex.FromAbsoluteIndex(i, act.Sprite, act.Sprite.Images[i]);
										}
									}
								   
									if (!sprIndex.Valid) {
										sprIndex = act.Sprite.InsertAny(image);
									}
								   
									int offsetX = (int) ((int) ((box.Max.X - box.Min.X + 1) / 2) + box.Min.X);
									int offsetY = (int) ((int) ((box.Max.Y - box.Min.Y + 1) / 2) + box.Min.Y);
									var layer = new Layer(sprIndex);
								   
									layer.OffsetX = offsetX;
									layer.OffsetY = offsetY;
								   
									frame.Layers.Clear();
									frame.Layers.Add(layer);
									index++;
								}
							}
						   
							// Removes unused sprites - old way, older versions have a bug
							for (int i = act.Sprite.Images.Count - 1; i >= 0 ; i--) {
								if (act.FindUsageOf(i).Count == 0) {
									var type = act.Sprite.Images[i].GrfImageType;
									var relativeIndex = act.Sprite.AbsoluteToRelative(i, type == GrfImageType.Indexed8 ? 0 : 1);
									act.Sprite.Remove(relativeIndex, type);
								   
									if (type == GrfImageType.Indexed8) {
										act.AllLayers(layer => {
											if ((layer.IsIndexed8() && type == GrfImageType.Indexed8) ||
												(layer.IsBgra32() && type == GrfImageType.Bgra32)) {
												if (layer.SpriteIndex == relativeIndex) {
													layer.SpriteIndex = -1;
												}
											}
										});
									}
				   
									act.Sprite.ShiftIndexesAbove(act, type, -1, relativeIndex);
								}
							}
						}
						finally {
							index = count;
						}
					}));
				}, "MyCustomScript", true);
			}
			catch (Exception err) {
				act.Commands.CancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.End();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}
	   
		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}
	}
}