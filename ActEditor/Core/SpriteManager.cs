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
using Utilities.Extension;
using Utilities.Services;

namespace ActEditor.Core {
	public enum SpriteManagerStatus {
		Ready,
		Cancel,
	}

	/// <summary>
	/// Used to manage the interactions from the SpriteEditor.
	/// </summary>
	public class SpriteManager {
		public static int SpriteConverterOption = -1;
		private TabAct _actEditor;
		private List<SpriteEditMode> _disabledModes = new List<SpriteEditMode>();
		public SpriteManagerStatus Status = SpriteManagerStatus.Ready;

		private List<GrfImage> _images {
			get { return _actEditor.Act.Sprite.Images; }
		}

		private Spr _sprite {
			get { return _actEditor.Act.Sprite; }
		}

		public void Init(TabAct actEditor) {
			_actEditor = actEditor;
		}

		public void Execute(SpriteIndex index, GrfImage image, SpriteEditMode mode) {
			bool isDelayed = _actEditor.Act.Commands.IsDelayed;

			try {
				if (!isDelayed)
					_actEditor.Act.Commands.ActEditBegin("Sprite: " + mode);

				_execute(index, image, mode);
			}
			catch {
				if (!isDelayed)
					_actEditor.Act.Commands.ActCancelEdit();

				throw;
			}
			finally {
				if (!isDelayed)
					_actEditor.Act.Commands.ActEditEnd();
			}
		}

		public void _execute(SpriteIndex index, GrfImage image, SpriteEditMode mode) {
			image = image ?? _sprite.GetImage(index);

			switch (mode) {
				case SpriteEditMode.Export:
					Export(index);
					break;
				case SpriteEditMode.Add:
					AddImage(image);
					break;
				case SpriteEditMode.ReplaceFlipVertical:
				case SpriteEditMode.ReplaceFlipHorizontal:
					FlipImage(index, mode == SpriteEditMode.ReplaceFlipHorizontal ? FlipDirection.Horizontal : FlipDirection.Vertical);
					break;
				case SpriteEditMode.Insert:
				case SpriteEditMode.Before:
					InsertImage(index, image, adjustLayerReferences: true);
					break;
				case SpriteEditMode.Convert:
					ConvertImage(index);
					break;
				case SpriteEditMode.After:
					InsertImage(index.GetAbsoluteIndex(_sprite) + 1, image, adjustLayerReferences: true);
					break;
				case SpriteEditMode.Remove:
					RemoveImage(index, adjustLayerReferences: true);
					break;
				case SpriteEditMode.Replace:
					ReplaceImage(index, image);
					break;
			}

			_visualUpdate();
		}

		public void ReplaceImage(SpriteIndex index, GrfImage image) {
			var spriteImage = _sprite.GetImage(index);

			if (spriteImage == null) {
				_actEditor.Act.Commands.SpriteAdd(_images.Last().GrfImageType == GrfImageType.Indexed8 ? _convertToAny(image) : _getImage(image, GrfImageType.Bgra32));
			}
			else {
				GrfImage imageConverted = _getImage(image, index.Type);
				int absoluteIndex = index.GetAbsoluteIndex(_sprite);
				Spr spr = _sprite;

				_actEditor.Act.Commands.Backup(act => {
					foreach (var layer in act.GetAllLayers()) {
						if (layer.GetAbsoluteSpriteId(spr) == absoluteIndex) {
							layer.Width = imageConverted.Width;
							layer.Height = imageConverted.Height;
						}
					}
				}, "Image adjustment");

				_actEditor.Act.Commands.SpriteReplaceAt(index, imageConverted);
			}
		}

		public void RemoveImage(SpriteIndex index, bool adjustLayerReferences) {
			var image = _sprite.GetImage(index);
			
			if (adjustLayerReferences) {
				_actEditor.Act.Commands.Backup(act => {
					foreach (var layer in act.GetAllLayers()) {
						if (layer.IsImageTypeValid(image)) {
							if (layer.SpriteIndex > index.Index) {
								layer.SpriteIndex--;
							}
							else if (layer.SpriteIndex == index.Index) {
								layer.SpriteIndex = -1;
								layer.Width = 0;
								layer.Height = 0;
							}
						}
					}
				}, "Index adjustment");
			}

			_actEditor.Act.Commands.SpriteRemove(index);
		}

		public void ConvertImage(SpriteIndex index) {
			var image = _sprite.GetImage(index);
			var imageCopy = image.Copy();
			int absoluteIndex = index.GetAbsoluteIndex(_sprite);

			GrfImage imageConverted = _getImage(imageCopy, imageCopy.GrfImageType == GrfImageType.Indexed8 ? GrfImageType.Bgra32 : GrfImageType.Indexed8);
			int relativeNewIndex = imageConverted.GrfImageType == GrfImageType.Indexed8 ? _sprite.NumberOfIndexed8Images : _sprite.NumberOfBgra32Images;
			Spr spr = _sprite;

			_actEditor.Act.Commands.Backup(act => {
				SpriteTypes type = imageConverted.GrfImageType == GrfImageType.Indexed8 ? SpriteTypes.Indexed8 : SpriteTypes.Bgra32;

				foreach (Layer layer in act.GetAllLayers().Where(layer => layer.SprSpriteIndex == index)) {
					layer.SpriteType = type;
					layer.SpriteIndex = relativeNewIndex;
				}

				_actEditor.Act.Sprite.Remove(absoluteIndex, act, EditOption.KeepCurrentIndexes);
				_actEditor.Act.Sprite.InsertAny(imageConverted);
				_actEditor.Act.Sprite.ShiftIndexesAbove(act, image.GrfImageType, -1,
					_sprite.AbsoluteToRelative(absoluteIndex, image.GrfImageType == GrfImageType.Indexed8 ? 0 : 1));
			}, "Sprite convert");
		}

		public void InsertImage(int absoluteIndex, GrfImage toAddImage, bool adjustLayerReferences = true) {
			InsertImage(SpriteIndex.FromAbsoluteIndex(absoluteIndex, _sprite), toAddImage, adjustLayerReferences);
		}

		public void InsertImage(SpriteIndex insertIndex, GrfImage toAddImage, bool adjustLayerReferences = true) {
			_validateActEditOnly();
			
			var image = _sprite.GetImage(insertIndex);
			var imageCopy = toAddImage.Copy();
			int absoluteIndex = insertIndex.GetAbsoluteIndex(_sprite);

			if (insertIndex.Type == GrfImageType.Bgra32 && insertIndex.Index == 0) {
				imageCopy = _convertToAny(imageCopy);
			}
			else {
				imageCopy = _getImage(imageCopy, insertIndex.Type);
			}

			Spr spr = _sprite;

			if (adjustLayerReferences) {
				_actEditor.Act.Commands.Backup(act => {
					foreach (var layer in act.GetAllLayers()) {
						if (layer.IsImageTypeValid(imageCopy)) {
							if (layer.GetAbsoluteSpriteId(spr) >= absoluteIndex) {
								layer.SpriteIndex++;
							}
						}
					}
				});
			}

			_actEditor.Act.Commands.SpriteInsert(absoluteIndex, imageCopy);
		}

		public void FlipImage(SpriteIndex index, FlipDirection direction) {
			_actEditor.Act.Commands.SpriteFlip(index.GetAbsoluteIndex(_sprite), direction);
		}

		public SpriteIndex AddImage(GrfImage image) {
			var nImage = _convertToAny(image);

			var idx = _sprite.Exists(nImage);

			if (!idx.Valid) {
				_actEditor.Act.Commands.SpriteAdd(nImage);
				idx = _sprite.Exists(nImage);
			}

			return idx;
		}

		public void Export(SpriteIndex index) {
			GrfImage image = _sprite.GetImage(index);
			var path = image.SaveTo(String.Format("image_{0:000}{1}", index.GetAbsoluteIndex(_sprite), index.Type == GrfImageType.Indexed8 ? ".bmp" : ".png"), ActEditorConfiguration.ExtractSetting);

			if (path != null) {
				if (File.Exists(path)) {
					OpeningService.FileOrFolder(path);
				}
			}
		}

		private void _validateActEditOnly() {
			//if (!_actEditor.Act.Commands.IsActEdit)
			//	throw new InvalidOperationException("Act object must be in ActEdit mode to use this function, with Act.Commands.ActEditBegin().");
		}

		private void _visualUpdate() {
			_actEditor._rendererPrimary.Update();
			_actEditor._layerEditor.Update();
			_actEditor.Focus();
		}

		private IEnumerable<GrfColor> _getUsedColors(GrfImage image) {
			HashSet<byte> usedPixels = new HashSet<byte>();

			for (int i = 0; i < image.Pixels.Length; i++) {
				usedPixels.Add(image.Pixels[i]);
			}

			usedPixels.Remove(0);

			HashSet<GrfColor> colors = new HashSet<GrfColor>();

			foreach (byte b in usedPixels) {
				colors.Add(new GrfColor(image.Palette, b * 4));
			}

			return colors;
		}

		private GrfImage _getConvertedImage(GrfImage image, bool enableBgra32Convert = true) {
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

					if (hasAllBeenFound) {
						image.Palette[0] = palette[0];
						image.Palette[1] = palette[1];
						image.Palette[2] = palette[2];
						image.Palette[3] = palette[3];
						image.Convert(new Indexed8FormatConverter { ExistingPalette = _sprite.Palette.BytePalette, Options = Indexed8FormatConverter.PaletteOptions.UseExistingPalette });
						//image.SetPalette(ref palette);
						return image;
					}
				}

				if (SpriteConverterOption == -2)
					throw new OperationCanceledException("Converter cancelled");

				if (image.GrfImageType != GrfImageType.Indexed8) {
					image.Convert(new Bgra32FormatConverter());
				}

				bool repeat = SpriteConverterOption > -1;

				if (repeat) {
					GrfImage.SprConvertMode mode = GrfImage.SprConvertMode.Original;

					switch (SpriteConverterOption) {
						case 0:
							mode = GrfImage.SprConvertMode.Original;
							break;
						case 1:
							mode = GrfImage.SprConvertMode.BestMatch;
							break;
						case 2:
							mode = GrfImage.SprConvertMode.MergeOld;
							break;
						case 3:
							mode = GrfImage.SprConvertMode.Bgra32;
							break;
						case 4:
							mode = GrfImage.SprConvertMode.MergeRgb;
							break;
						case 5:
							mode = GrfImage.SprConvertMode.MergeLab;
							break;
					}

					image = GrfImage.SprConvert(_sprite, image, ActEditorConfiguration.UseDithering, (GrfImage.SprTransparencyMode)ActEditorConfiguration.TransparencyMode, mode);

					if (image.GrfImageType == GrfImageType.Indexed8) {
						image.Palette[3] = 0;
						_actEditor.Act.Commands.SpriteSetPalette(image.Palette);
					}
				}
				else {
					if (!enableBgra32Convert) {
						if (ActEditorConfiguration.FormatConflictOption == 3) {
							ActEditorConfiguration.FormatConflictOption = 1;
						}
					}

					SpriteConverterFormatDialog dialog = new SpriteConverterFormatDialog(_sprite.Palette.BytePalette, image, _sprite, SpriteConverterOption);
					
					if (!enableBgra32Convert) {
						dialog._rbBgra32.IsEnabled = false;
					}

					dialog.Owner = WpfUtilities.TopWindow;

					var diagResult = dialog.ShowDialog();

					if (diagResult == true) {
						SpriteConverterOption = dialog.RepeatOption;
						image = dialog.Result;

						if (image.GrfImageType == GrfImageType.Indexed8) {
							image.Palette[3] = 0;
							_actEditor.Act.Commands.SpriteSetPalette(image.Palette);
						}
					}
					else {
						SpriteConverterOption = dialog.RepeatOption;
						throw new OperationCanceledException("Converter cancelled");
					}
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
					image = _getConvertedImage(image, enableBgra32Convert: false);

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
					image = _getConvertedImage(image, enableBgra32Convert: false);

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

		public void Begin() {
			Status = SpriteManagerStatus.Ready;
		}

		public void End() {
			Status = SpriteManagerStatus.Ready;
		}

		public void Insert(int absoluteIndex, IEnumerable<GrfImage> images) {
			try {
				_actEditor.Act.Commands.BeginNoDelay();

				foreach (var image in images) {
					_insert(absoluteIndex, image);
				}
			}
			catch (OperationCanceledException) {
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
			finally {
				_actEditor.Act.Commands.End();
			}
		}

		private void _insert(int index, GrfImage image) {
			throw new NotImplementedException();
		}

		public void InsertImages(int absoluteIndex, List<string> files) {
			try {
				Begin();
				SpriteConverterOption = -1;

				_actEditor.Act.Commands.ActEditBegin("Sprite: " + SpriteEditMode.Insert.ToString());

				foreach (var file in files) {
					if (file.IsExtension(".bmp", ".tga", ".jpg", ".png")) {
						Execute(SpriteIndex.FromAbsoluteIndex(absoluteIndex, _sprite), new GrfImage(file), SpriteEditMode.Insert);
					}
					else if (file.IsExtension(".spr")) {
						List<GrfImage> images = new Spr(file).Images;
						images.Reverse();

						foreach (var image in images) {
							Execute(SpriteIndex.FromAbsoluteIndex(absoluteIndex, _sprite), image, SpriteEditMode.Insert);
						}
					}
				}
			}
			catch (OperationCanceledException) {
			}
			catch {
				_actEditor.Act.Commands.ActCancelEdit();
				throw;
			}
			finally {
				_actEditor.Act.Commands.ActEditEnd();
				End();
			}
		}
	}

	public enum SpriteEditMode {
		Replace,
		Remove,
		Before,
		After,
		Insert,
		ReplaceFlipHorizontal,
		ReplaceFlipVertical,
		Export,
		Add,
		Convert,
		Usage,
	}
}