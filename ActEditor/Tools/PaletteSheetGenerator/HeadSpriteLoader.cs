using GRF;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using System;
using System.Collections.Generic;
using System.IO;
using Utilities.Extension;
using Utilities.Services;

namespace ActEditor.Tools.PaletteSheetGenerator {
	public class HeadSpriteLoader {
		public class HeadSpriteResource : SpriteResource {
			private string _headId;
			public override string DisplayName { get; set; }
			public override string Sprite { get; set; }

			public string GetLoadPath() {
				return Sprite;
			}

			public Act GetAct(GrfHolder grf) {
				var relativePath = GetLoadPath();
				var entryAct = grf.FileTable.TryGet(relativePath);
				var entrySpr = grf.FileTable.TryGet(relativePath.ReplaceExtension(".spr"));

				if (entryAct == null || entrySpr == null)
					return null;

				return new Act(entryAct, entrySpr);
			}

			public string HeadId {
				get {
					if (_headId == null)
						_headId = _getHeadId();
					return _headId;
				}
			}

			private string _getHeadId() {
				try {
					var path = Path.GetFileNameWithoutExtension(Sprite);
					return path.Split('_')[0];
				}
				catch {
					return "0";
				}
			}

			public string GetPalettePath(int palId, Genders gender) {
				string genderString = gender == Genders.Male ? GrfStrings.GenderMale : GrfStrings.GenderFemale;
				return EncodingService.FromAnyToDisplayEncoding($@"data\palette\¸Ó¸®\¸Ó¸®{HeadId}_{genderString}_{palId}.pal");
			}

			public byte[] GetPalette(int palId, Genders gender, GrfHolder grf) {
				string path = GetPalettePath(palId, gender);
				var entry = grf.FileTable.TryGet(path);
				return entry?.GetDecompressedData();
			}
		}

		public class LoadResult {
			public bool Success;
			public List<HeadSpriteResource> MaleResources = new List<HeadSpriteResource>();
			public List<HeadSpriteResource> FemaleResources = new List<HeadSpriteResource>();
		}

		public LoadResult Load(GrfHolder grf) {
			LoadResult result = new LoadResult();

			if (grf == null || !grf.IsOpened)
				return result;

			_load(@"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\³²\", grf, result, result.MaleResources, Genders.Male);
			_load(@"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\¿©\", grf, result, result.FemaleResources, Genders.Female);

			return result;
		}

		private void _load(string path, GrfHolder grf, LoadResult result, List<HeadSpriteResource> sprites, Genders gender) {
			var genderString = gender == Genders.Male ? GrfStrings.GenderMale : GrfStrings.GenderFemale;

			foreach (var entry_s in grf.FileTable.EntriesInDirectory(EncodingService.FromAnyToDisplayEncoding(path), SearchOption.TopDirectoryOnly)) {
				if (!entry_s.RelativePath.IsExtension(".act"))
					continue;

				var entry = entry_s;
				HeadSpriteResource sprite = new HeadSpriteResource();
				sprite.DisplayName = String.Format("Head Sprite #{0:000}", Path.GetFileName(entry.RelativePath).Replace(EncodingService.FromAnyToDisplayEncoding($"_{genderString}.act"), ""));
				sprite.Sprite = entry.RelativePath;
				sprites.Add(sprite);
			}
		}
	}
}
