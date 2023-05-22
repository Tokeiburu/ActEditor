using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.Dialogs;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.Image.Decoders;
using GrfToWpfBridge;
using TokeiLibrary;
using Utilities;
using Utilities.Services;

namespace ActEditor.Core {
	/// <summary>
	/// Used to manage the interactions from the SpriteEditor.
	/// </summary>
	public class SpriteManager {
		public static int SpriteConverterOption = -1;
		private TabAct _actEditor;
		private List<SpriteEditMode> _disabledModes = new List<SpriteEditMode>();

		private List<GrfImage> _images {
			get { return _actEditor.Act.Sprite.Images; }
		}

		private Spr _sprite {
			get { return _actEditor.Act.Sprite; }
		}

		public void Init(TabAct actEditor) {
			_actEditor = actEditor;
		}

		public void Execute(int absoluteIndex, GrfImage image, SpriteEditMode mode) {
			if (image == null) {
				image = _images[absoluteIndex];
			}

			// The image is NOT converted
			int relativeIndex;
			GrfImage imageCopy;

			switch (mode) {
				case SpriteEditMode.Export:
					image.SaveTo(String.Format("image_{0:000}{1}", absoluteIndex, image.GrfImageType == GrfImageType.Indexed8 ? ".bmp" : ".png"), PathRequest.ExtractSetting);

					try {
						string path = PathRequest.ExtractSetting.Get() as string;

						if (path != null) {
							if (File.Exists(path)) {
								OpeningService.FileOrFolder(path);
							}
						}
					}
					catch (Exception err) {
						ErrorHandler.HandleException(err);
					}
					break;
				case SpriteEditMode.Add:
					try {
						_actEditor.Act.Commands.Begin();
						imageCopy = _convertToAny(image);
						_actEditor.Act.Commands.SpriteAdd(imageCopy);
					}
					catch (OperationCanceledException) {
					}
					catch (Exception err) {
						_actEditor.Act.Commands.CancelEdit();
						ErrorHandler.HandleException(err);
					}
					finally {
						_actEditor.Act.Commands.End();
					}
					break;
				case SpriteEditMode.ReplaceFlipVertical:
				case SpriteEditMode.ReplaceFlipHorizontal:
					_actEditor.Act.Commands.SpriteFlip(absoluteIndex, mode == SpriteEditMode.ReplaceFlipHorizontal ? FlipDirection.Horizontal : FlipDirection.Vertical);
					break;
				case SpriteEditMode.Before:
					relativeIndex = _images[absoluteIndex].GrfImageType == GrfImageType.Indexed8 ? absoluteIndex : absoluteIndex - _sprite.NumberOfIndexed8Images;
					imageCopy = image.Copy();

					bool isDelayed = _actEditor.Act.Commands.IsDelayed;

					try {
						if (!isDelayed)
							_actEditor.Act.Commands.Begin();

						if (relativeIndex == 0 && _images[absoluteIndex].GrfImageType == GrfImageType.Bgra32) {
							imageCopy = _convertToAny(imageCopy);
						}
						else {
							imageCopy = _getImage(imageCopy, _images[absoluteIndex].GrfImageType);
						}

						Spr spr = _sprite;

						_actEditor.Act.Commands.Backup(act => {
							foreach (var layer in act.GetAllLayers()) {
								if (layer.IsImageTypeValid(imageCopy)) {
									if (layer.GetAbsoluteSpriteId(spr) >= absoluteIndex) {
										layer.SpriteIndex++;
									}
								}
							}
						}, "Index adjustment");

						_actEditor.Act.Commands.SpriteInsert(absoluteIndex, imageCopy);
					}
					catch (OperationCanceledException) {
					}
					catch (Exception err) {
						if (!isDelayed)
							_actEditor.Act.Commands.CancelEdit();
						else
							throw;

						ErrorHandler.HandleException(err);
					}
					finally {
						if (!isDelayed)
							_actEditor.Act.Commands.End();
					}
					break;
				case SpriteEditMode.Convert:
					try {
						imageCopy = image.Copy();
						GrfImage imageConverted = _getImage(imageCopy, imageCopy.GrfImageType == GrfImageType.Indexed8 ? GrfImageType.Bgra32 : GrfImageType.Indexed8);
						int relativeNewIndex = imageConverted.GrfImageType == GrfImageType.Indexed8 ? _sprite.NumberOfIndexed8Images : _sprite.NumberOfBgra32Images;
						Spr spr = _sprite;

						_actEditor.Act.Commands.Begin();
						_actEditor.Act.Commands.Backup(act => {
							SpriteTypes type = imageConverted.GrfImageType == GrfImageType.Indexed8 ? SpriteTypes.Indexed8 : SpriteTypes.Bgra32;

							foreach (Layer layer in act.GetAllLayers().Where(layer => layer.GetAbsoluteSpriteId(spr) == absoluteIndex)) {
								layer.SpriteType = type;
								layer.SpriteIndex = relativeNewIndex;
							}

							_actEditor.Act.Sprite.Remove(absoluteIndex, act, EditOption.KeepCurrentIndexes);
							_actEditor.Act.Sprite.InsertAny(imageConverted);
							_actEditor.Act.Sprite.ShiftIndexesAbove(act, image.GrfImageType, -1,
								_sprite.AbsoluteToRelative(absoluteIndex, image.GrfImageType == GrfImageType.Indexed8 ? 0 : 1));
						}, "Sprite convert", true);
					}
					catch (OperationCanceledException) {
					}
					catch (Exception err) {
						_actEditor.Act.Commands.CancelEdit();
						ErrorHandler.HandleException(err);
					}
					finally {
						_actEditor.Act.Commands.End();
					}
					break;
				case SpriteEditMode.After:
					imageCopy = image.Copy();

					isDelayed = _actEditor.Act.Commands.IsDelayed;

					try {
						if (!isDelayed)
							_actEditor.Act.Commands.Begin();

						if (_images[absoluteIndex].GrfImageType == GrfImageType.Indexed8 && absoluteIndex == _sprite.NumberOfIndexed8Images - 1) {
							imageCopy = _convertToAny(imageCopy);
						}
						else {
							imageCopy = _getImage(imageCopy, _images[absoluteIndex].GrfImageType);
						}

						Spr spr = _sprite;

						_actEditor.Act.Commands.Backup(act => {
							foreach (var layer in act.GetAllLayers()) {
								if (layer.IsImageTypeValid(imageCopy)) {
									if (layer.GetAbsoluteSpriteId(spr) > absoluteIndex) {
										layer.SpriteIndex++;
									}
								}
							}
						}, "Index adjustment");

						_actEditor.Act.Commands.SpriteInsert(absoluteIndex + 1, imageCopy);
					}
					catch (OperationCanceledException) {
					}
					catch (Exception err) {
						if (!isDelayed)
							_actEditor.Act.Commands.CancelEdit();
						else
							throw;

						ErrorHandler.HandleException(err);
					}
					finally {
						if (!isDelayed)
							_actEditor.Act.Commands.End();
					}
					break;
				case SpriteEditMode.Remove:
					try {
						relativeIndex = image.GrfImageType == GrfImageType.Indexed8 ? absoluteIndex : absoluteIndex - _sprite.NumberOfIndexed8Images;

						_actEditor.Act.Commands.Begin();
						_actEditor.Act.Commands.Backup(act => {
							foreach (var layer in act.GetAllLayers()) {
								if (layer.IsImageTypeValid(image)) {
									if (layer.SpriteIndex > relativeIndex) {
										layer.SpriteIndex--;
									}
									else if (layer.SpriteIndex == relativeIndex) {
										layer.SpriteIndex = -1;
										layer.Width = 0;
										layer.Height = 0;
									}
								}
							}
						}, "Index adjustment");

						_actEditor.Act.Commands.SpriteRemove(absoluteIndex);
					}
					catch (OperationCanceledException) {
					}
					catch (Exception err) {
						_actEditor.Act.Commands.CancelEdit();
						ErrorHandler.HandleException(err);
					}
					finally {
						_actEditor.Act.Commands.End();
					}
					break;
				case SpriteEditMode.Replace:
					isDelayed = _actEditor.Act.Commands.IsDelayed;

					try {
						if (!isDelayed)
							_actEditor.Act.Commands.Begin();

						if (absoluteIndex >= _images.Count) {
							GrfImage last = _images.Last();
							_actEditor.Act.Commands.SpriteAdd(last.GrfImageType == GrfImageType.Indexed8 ? _convertToAny(image) : _getImage(image, GrfImageType.Bgr32));
						}
						else {
							GrfImage imageConverted = _getImage(image, _images[absoluteIndex].GrfImageType);
							Spr spr = _sprite;

							_actEditor.Act.Commands.Backup(act => {
								foreach (var layer in act.GetAllLayers()) {
									if (layer.GetAbsoluteSpriteId(spr) == absoluteIndex) {
										layer.Width = imageConverted.Width;
										layer.Height = imageConverted.Height;
									}
								}
							}, "Image adjustment");

							_actEditor.Act.Commands.SpriteReplaceAt(absoluteIndex, imageConverted);
						}
					}
					catch (OperationCanceledException) {
					}
					catch (Exception err) {
						if (!isDelayed) {
							_actEditor.Act.Commands.CancelEdit();
							ErrorHandler.HandleException(err);
						}
						else
							throw;
					}
					finally {
						if (!isDelayed)
							_actEditor.Act.Commands.End();
					}
					break;
			}

			_visualUpdate();
		}

		private void _visualUpdate() {
			_actEditor._rendererPrimary.Update();
			_actEditor._layerEditor.Update();
			_actEditor.SelectionEngine.RefreshSelection();
			_actEditor.Focus();
		}

		private IEnumerable<GrfColor> _getUsedColors(GrfImage image) {
			HashSet<byte> usedPixels = new HashSet<byte>();

			for (int i = 0; i < image.Pixels.Length; i++) {
				usedPixels.Add(image.Pixels[i]);
			}

			HashSet<GrfColor> colors = new HashSet<GrfColor>();

			foreach (byte b in usedPixels) {
				colors.Add(new GrfColor(image.Palette, b * 4));
			}

			return colors;
		}

		private GrfImage _getConvertedImage(GrfImage image) {
			byte[] palette = _sprite.Palette == null ? null : _sprite.Palette.BytePalette;

			if (_paletteIsSet() && image.GrfImageType == GrfImageType.Indexed8 && Methods.ByteArrayCompare(palette, 4, 1020, image.Palette, 4)) {
				image.SetPalette(ref palette);
			}
			else {
				if (SpriteConverterOption != 0 && image.GrfImageType == GrfImageType.Indexed8) {
					// Check if the image has all the same colors found in the palette
					IEnumerable<GrfColor> usedColors = _getUsedColors(image);
					HashSet<GrfColor> paletteColors = _getPaletteColors();

					bool hasAllBeenFound = true;

					foreach (GrfColor color in usedColors) {
						if (!paletteColors.Contains(color)) {
							hasAllBeenFound = false;
							break;
						}
					}

					// Be careful here, if all colors have been found and the transparent pixel is different, then the palettes are different
					if (hasAllBeenFound) {
						if (image.Palette[1] != palette[1] ||
						    image.Palette[2] != palette[2] ||
						    image.Palette[3] != palette[3]) {
							hasAllBeenFound = false;
						}
					}

					if (hasAllBeenFound) {
						image.Palette[0] = palette[0];
						image.Palette[1] = palette[1];
						image.Palette[2] = palette[2];
						image.Palette[3] = palette[3];
						image.Convert(new Indexed8FormatConverter {ExistingPalette = _sprite.Palette.BytePalette, Options = Indexed8FormatConverter.PaletteOptions.UseExistingPalette});
						//image.SetPalette(ref palette);
						return image;
					}
				}

				if (SpriteConverterOption == -2)
					throw new OperationCanceledException("Converter cancelled");

				if (image.GrfImageType != GrfImageType.Indexed8) {
					image.Convert(new Bgra32FormatConverter());
				}

				SpriteConverterFormatDialog dialog = new SpriteConverterFormatDialog(_sprite.Palette.BytePalette, image, _sprite, SpriteConverterOption);
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					SpriteConverterOption = dialog.RepeatOption;
					image = dialog.Result;

					if (image.GrfImageType == GrfImageType.Indexed8) {
						_actEditor.Act.Commands.SpriteSetPalette(image.Palette);
					}
				}
				else {
					SpriteConverterOption = dialog.RepeatOption;
					throw new OperationCanceledException("Converter cancelled");
				}
			}

			return image;
		}

		private HashSet<GrfColor> _getPaletteColors() {
			byte[] palette = _sprite.Palette.BytePalette;

			HashSet<GrfColor> colors = new HashSet<GrfColor>();

			// Fix transparency color since it will be matched with other imported palettes
			colors.Add(new GrfColor(255, palette[0], palette[1], palette[2]));

			for (int i = 4; i < 1024; i += 4) {
				colors.Add(new GrfColor(palette, i));
			}

			return colors;
		}

		private GrfImage _getImage(GrfImage image, GrfImageType desiredType) {
			if (image.GrfImageType == GrfImageType.Indexed8 && desiredType == GrfImageType.Indexed8) {
				// Make sure the image uses the current palette
				if (_paletteIsSet()) {
					image = _getConvertedImage(image);

					if (image.GrfImageType != GrfImageType.Indexed8) {
						image.Convert(new Indexed8FormatConverter { ExistingPalette = _sprite.Palette.BytePalette, Options = Indexed8FormatConverter.PaletteOptions.UseExistingPalette });
					}
				}

				return image;
			}

			if (desiredType == GrfImageType.Indexed8) {
				if (!_paletteIsSet()) {
					image.Convert(new Indexed8FormatConverter {Options = Indexed8FormatConverter.PaletteOptions.AutomaticallyGeneratePalette});
					_actEditor.Act.Commands.SpriteSetPalette(image.Palette);
				}
				else {
					image = _getConvertedImage(image);

					if (image.GrfImageType != GrfImageType.Indexed8) {
						image.Convert(new Indexed8FormatConverter {ExistingPalette = _sprite.Palette.BytePalette, Options = Indexed8FormatConverter.PaletteOptions.UseExistingPalette});
					}
				}

				return image;
			}

			if (desiredType == GrfImageType.Bgra32 && image.GrfImageType == GrfImageType.Indexed8) {
				image.Palette[3] = 0;
				image.Convert(new Bgra32FormatConverter());
			}

			image.Convert(new Bgra32FormatConverter());

			return image;
		}

		private bool _paletteIsSet() {
			return _sprite.Palette != null && _sprite.Palette.BytePalette != null;
		}

		private GrfImage _convertToAny(GrfImage image) {
			if (image.GrfImageType == GrfImageType.Indexed8) {
				// Make sure the image uses the current palette
				if (!_paletteIsSet()) {
					//image.Convert(new Indexed8FormatConverter { Options = Indexed8FormatConverter.PaletteOptions.AutomaticallyGeneratePalette });
					_actEditor.Act.Commands.SpriteSetPalette(image.Palette);
				}
				else {
					image = _getConvertedImage(image);
				}

				return image;
			}

			image.Convert(new Bgra32FormatConverter());
			return image;
		}

		public void AddDisabledMode(SpriteEditMode mode) {
			_disabledModes.Add(mode);
		}

		public bool IsModeDisabled(SpriteEditMode mode) {
			return _disabledModes.Any(p => p == mode);
		}
	}

	public enum SpriteEditMode {
		Replace,
		Remove,
		Before,
		After,
		ReplaceFlipHorizontal,
		ReplaceFlipVertical,
		Export,
		Add,
		Convert,
		Usage
	}
}