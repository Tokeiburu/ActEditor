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
using GRF.Core;
using GRF.IO;
using GRF.System;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Utilities;
using Utilities.Extension;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;
using Point = System.Windows.Point;

namespace Scripts {
    public class Script : IActScript {
		public object DisplayName {
			get { return "Trim sprite images (ajust positions)"; }
		}
		
		public string Group {
			get { return "Scripts"; }
		}
		
		public string InputGesture {
			get { return null; }
		}
		
		public string Image {
			get { return "cs_pen.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			byte tolerate = 0x10;	// This setting is used for bgra32/semi-transparent images. If the transparency is below 0x10, it will be considered a transparent pixel.
			
			try {
				act.Commands.BeginNoDelay();
				act.Commands.Backup(_ => {
					var planes = new Dictionary<ActIndex, Plane>();
					
					act.AllLayersAdv((index, layer) => {
						planes[index] = _layerToPlane(act, layer);
					});
					
					var spriteCopy = new Spr(act.Sprite);
					
					for (int i = 0; i < act.Sprite.Images.Count; i++) {
						var img = act.Sprite.Images[i];
						var oriImg = spriteCopy.Images[i];
						
						var bpp = img.GetBpp();
					
						// Leave at least 1 pixel border
						int left = _getLengthTrim(img, TrimDirection.Left, bpp, tolerate) - 1;
						int right = _getLengthTrim(img, TrimDirection.Right, bpp, tolerate) - 1;
						int top = _getLengthTrim(img, TrimDirection.Top, bpp, tolerate) - 1;
						int bottom = _getLengthTrim(img, TrimDirection.Bottom, bpp, tolerate) - 1;
						
						int[] trims = new int[] {
							Math.Max(0, left), 
							Math.Max(0, right), 
							Math.Max(0, top), 
							Math.Max(0, bottom)
						};
						
						for (int j = 0; j < 4; j += 2) {
							switch(j) {
								case 0: img.Crop(trims[j], 0, trims[j + 1], 0); break;
								case 2: img.Crop(0, trims[j], 0, trims[j + 1]); break;
							}
							
							act.AllLayersAdv((index, layer) => {
								if (layer.SpriteIndex < 0) return;
								var aspi = layer.GetAbsoluteSpriteId(act.Sprite);
								var image = layer.GetImage(act.Sprite);
								
								if (aspi != i)
									return;
								
								var previousCenter = _getCenter(planes[index]);
								
								// Need to adjust because... layers aren't centered with odd width/height images.
								// Frankly the formula is quite a nightmare.
								float specialAdjust = 0;
								Plane plane = new Plane(image.Width, image.Height);
								
								if ((image.Width % 2) == 1) {
									plane.Translate(0.5f, 0);
								}
								
								plane.ScaleX(layer.ScaleX * (layer.Mirror ? -1f : 1f));
								plane.ScaleY(layer.ScaleY);
								plane.RotateZ(layer.Rotation);
								
								switch(j) {
									case 0:
										if ((oriImg.Width - image.Width) % 2 == 1) {
											if (layer.Mirror) {
												specialAdjust = (image.Width % 2 == 1) ? -1.5f : 1.5f;
											}
											else {
												specialAdjust = (image.Width % 2 == 1) ? 0.5f : -0.5f;
											}
										}
										
										float translateX = (trims[j] - trims[j + 1]) / 2f + specialAdjust;
										plane.Translate(layer.OffsetX + translateX + (layer.Mirror ? -(image.Width + 1) % 2 : 0), layer.OffsetY);
										break;
									case 2:
										if ((oriImg.Height - image.Height) % 2 == 1) {
											specialAdjust = (image.Height % 2 == 1) ? 0.5f + 0.5f * layer.ScaleY : -0.5f - 0.5f * layer.ScaleY;
										}
										
										float translateY = (trims[j] - trims[j + 1]) / 2f + specialAdjust;
										plane.Translate(layer.OffsetX + (layer.Mirror ? -(image.Width + 1) % 2 : 0), layer.OffsetY + translateY);
										break;
								}
								
								var newCenter = _getCenter(plane);
								var diff = newCenter - previousCenter;
								
								layer.OffsetX += (int)Math.Round((diff.X * layer.ScaleX * (layer.Mirror ? -1 : 1)), MidpointRounding.AwayFromZero);
								layer.OffsetY += (int)Math.Round(diff.Y * layer.ScaleY, MidpointRounding.AwayFromZero);
								planes[index] = _layerToPlane(act, layer);
							});
						}
					}
				}, "Trim Images", true);
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
			return true;
			//return act != null;
		}
		
		private int _getLengthTrim(GrfImage image, TrimDirection direction, int bpp, byte tolerance) {
			bool stop = false;
			int toRemove = 0;
			FlipDirection stride = direction == TrimDirection.Top || direction == TrimDirection.Bottom ? FlipDirection.Horizontal : FlipDirection.Vertical;

			if (stride == FlipDirection.Vertical) {
				for (int x = direction == TrimDirection.Left ? 0 : image.Width - 1; x < image.Width && x >= 0;) {
					for (int y = 0; y < image.Height; y++) {
						if (bpp == 1) {
							if (image.Pixels[y * image.Width + x] != 0) {
								stop = true;
								break;
							}
						}
						else if (bpp == 4) {
							if (image.Pixels[bpp * (y * image.Width + x) + 3] > tolerance) {
								stop = true;
								break;
							}
						}
					}

					if (stop) {
						if (direction == TrimDirection.Right)
							toRemove = image.Width - x - 1;
						else
							toRemove = x;
						break;
					}
					
					x = direction == TrimDirection.Left ? x + 1 : x - 1;
				}
			}
			else {
				for (int y = direction == TrimDirection.Top ? 0 : image.Height - 1; y < image.Height && y >= 0;) {
					for (int x = 0; x < image.Width; x++) {
						if (bpp == 1) {
							if (image.Pixels[y * image.Width + x] != 0) {
								stop = true;
								break;
							}
						}
						else if (bpp == 4) {
							if (image.Pixels[bpp * (y * image.Width + x) + 3] > tolerance) {
								stop = true;
								break;
							}
						}
					}

					if (stop) {
						if (direction == TrimDirection.Bottom)
							toRemove = image.Height - y - 1;
						else
							toRemove = y;
						break;
					}
					
					y = direction == TrimDirection.Top ? y + 1 : y - 1;
				}
			}

			return toRemove;
		}
		
		private Plane _layerToPlane(Act act, Layer layer) {
			if (layer.SpriteIndex < 0) return null;
			var aspi = layer.GetAbsoluteSpriteId(act.Sprite);
			var image = layer.GetImage(act.Sprite);
			
			Plane plane = new Plane(image.Width, image.Height);
			
			if ((image.Width % 2) == 1) {
				plane.Translate(0.5f, 0);
			}
			
			plane.ScaleX(layer.ScaleX * (layer.Mirror ? -1f : 1f));
			plane.ScaleY(layer.ScaleY);
			plane.RotateZ(layer.Rotation);
			plane.Translate(layer.OffsetX + (layer.Mirror ? -(image.Width + 1) % 2 : 0), layer.OffsetY);
			return plane;
		}
		
		private TkVector2 _getCenter(Plane plane) {
			TkVector2 center = new TkVector2();
			
			foreach(var point in plane.Points) {
				center += point;
			}
			
			return center / 4;
		}
	}
}
