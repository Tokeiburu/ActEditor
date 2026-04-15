using ActEditor.ApplicationConfiguration;
using ActEditor.Core.Scripting;
using GRF;
using GRF.Core;
using GRF.FileFormats.ActFormat;
using GRF.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TokeiLibrary;
using Utilities.Extension;
using Utilities.Parsers;
using Utilities.Parsers.Libconfig;
using Utilities.Services;

namespace ActEditor.Tools.PaletteSheetGenerator {
	public class BodySpritesLoader {
		public class BodySpriteResource : SpriteResource {
			private string _className;

			public string ClassGroup { get; set; }
			public bool IsMount { get; set; }
			public bool IsCostume { get; set; }
			public override string Sprite { get; set; }
			public string Palette { get; set; }
			public AllowedGenders AllowedGenders = AllowedGenders.Both;
			public override string DisplayName {
				get {
					if (_className == null) {
						_className = _getClassName();
					}

					return _className;
				}
				set => _className = value;
			}
			public override string ToolTip {
				get {
					if (string.IsNullOrEmpty(_toolTip))
						return DisplayName + "\r\nResource: " + Sprite + "\r\nPalette: " + Palette;

					return _toolTip;
				}
			}

			private string _getClassName() {
				var dirs = GrfPath.SplitDirectories(ClassGroup).ToList();
				string className = IsMount ? dirs[dirs.Count - 2] + " (" + dirs[dirs.Count - 1] + ")" : Path.GetFileName(ClassGroup);
				className = className.Replace("[4th] ", "");
				return className;
			}

			public string GetLoadPath(Genders gender) {
				string genderString = gender == Genders.Male ? GrfStrings.GenderMale : GrfStrings.GenderFemale;
				string output;

				if (IsCostume) {
					output = $@"data\sprite\ÀÎ°£Á·\¸öÅë\{genderString}\costume_1\{Sprite}_{genderString}_1.act";
				}
				else {
					output = $@"data\sprite\ÀÎ°£Á·\¸öÅë\{genderString}\{Sprite}_{genderString}.act";
				}

				return EncodingService.FromAnyToDisplayEncoding(output);
			}

			public Act GetAct(Genders gender, GrfHolder grf) {
				var relativePath = GetLoadPath(gender);
				var entryAct = grf.FileTable.TryGet(relativePath);
				var entrySpr = grf.FileTable.TryGet(relativePath.ReplaceExtension(".spr"));

				if (entryAct == null || entrySpr == null)
					return null;

				return new Act(entryAct, entrySpr);
			}

			public byte[] GetPalette(int index, Genders gender, GrfHolder grf) {
				string genderString = gender == Genders.Male ? GrfStrings.GenderMale : GrfStrings.GenderFemale;
				string path0;
				string path1;
				
				if (ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath) {
					path0 = $@"data\palette\¸ö\{Palette}_{genderString}_{index}.pal";
					path1 = $@"data\palette\¸ö\{Palette}_{index}.pal";
				}
				else {
					path0 = $@"data\palette\¸ö\{ActEditorConfiguration.PreviewSheetPredefinedPalettePath}_{genderString}_{index}.pal";
					path1 = $@"data\palette\¸ö\{ActEditorConfiguration.PreviewSheetPredefinedPalettePath}_{index}.pal";
				}

				var entry = grf.FileTable.TryGet(path0) ?? grf.FileTable.TryGet(path1);
				return entry?.GetDecompressedData();
			}

			public string GetPalettePath(int index, Genders gender) {
				string genderString = gender == Genders.Male ? GrfStrings.GenderMale : GrfStrings.GenderFemale;
				string path0;

				if (ActEditorConfiguration.PreviewSheetUsePredefinedPalettePath) {
					path0 = $@"data\palette\¸ö\{Palette}_{genderString}_{index}.pal";
				}
				else {
					path0 = $@"data\palette\¸ö\{ActEditorConfiguration.PreviewSheetPredefinedPalettePath}_{genderString}_{index}.pal";
				}

				return EncodingService.FromAnyToDisplayEncoding(path0);
			}
		}

		public class LoadResult {
			public bool Success;
			public Dictionary<string, BodySpriteResource> Resources = new Dictionary<string, BodySpriteResource>();
			public List<BodySpriteResource> MaleResources = new List<BodySpriteResource>();
			public List<BodySpriteResource> FemaleResources = new List<BodySpriteResource>();
		}

		public LoadResult Load() {
			LoadResult result = new LoadResult();

			_load("sprites.conf", result);

			if (ActEditorConfiguration.PreviewSheetUseOldConfig) {
				_load("sprites_old.conf", result);
			}

			result.MaleResources = result.Resources.Values.Where(p => p.AllowedGenders.HasFlag(AllowedGenders.Male)).ToList();
			result.FemaleResources = result.Resources.Values.Where(p => p.AllowedGenders.HasFlag(AllowedGenders.Female)).ToList();

			return result;
		}

		private void _load(string configFile, LoadResult result) {
			string path = GrfPath.Combine(ActEditorConfiguration.ProgramDataPath, ScriptLoader.OutputPath, configFile);

			if (!File.Exists(path)) {
				File.WriteAllBytes(path, ApplicationManager.GetResource(configFile));
			}

			var parser = new LibconfigParser(path, EncodingService.DisplayEncoding);
			var paletteConf = parser.Output["palette_conf"];

			foreach (var entry in paletteConf) {
				_loadBody(entry, result);
			}
		}

		private void _loadBody(ParserObject entry, LoadResult result) {
			BodySpriteResource resource;
			string classGroup = entry["class"];
			string sprite = entry["sprite"];

			if (!result.Resources.TryGetValue(classGroup, out resource)) {
				resource = new BodySpriteResource();
				resource.ClassGroup = classGroup;
				result.Resources[classGroup] = resource;
			}

			resource.Sprite = sprite;
			resource.Palette = entry["palette"];

			if (entry["ismount"] != null)
				resource.IsMount = Boolean.Parse(entry["ismount"] ?? "false");

			if (entry["isCostume"] != null)
				resource.IsCostume = Boolean.Parse(entry["isCostume"] ?? "false");

			if (entry["male"] != null) {
				bool v = Boolean.Parse(entry["male"]);

				if (v)
					resource.AllowedGenders &= ~AllowedGenders.Female;
				else
					resource.AllowedGenders &= ~AllowedGenders.Male;
			}

			if (entry["female"] != null) {
				bool v = Boolean.Parse(entry["female"]);

				if (v)
					resource.AllowedGenders &= ~AllowedGenders.Male;
				else
					resource.AllowedGenders &= ~AllowedGenders.Female;
			}
		}
	}
}
