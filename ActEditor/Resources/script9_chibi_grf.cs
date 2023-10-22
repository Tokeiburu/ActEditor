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
			get { return "Chibi Grf Utility"; }
		}
		
		public string Group {
			get { return "Scripts"; }
		}
		
		public string InputGesture {
			get { return "{Scripts.ChibiGrfUtility}"; }
		}
		
		public string Image {
			get { return "filter.png"; }
		}
		
		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;
			
			Exception backupErr = null;
			
			try {
				act.Commands.BeginNoDelay();
				act.Commands.Backup(_ => {
					try {
					// Act Editor 1.0.5+ required
					// The following script will resize the heads, weapons, 
					// shields, backpacks and wings to the mag value below.
					Utilities.Services.EncodingService.SetDisplayEncoding(1252);
					
					//=============================
					// Window creation for input
					//=============================
					var window = new Window();
					window.SizeToContent = SizeToContent.WidthAndHeight;
					window.MinWidth = 200;
					window.MinHeight = 100;
					window.Owner = WpfUtilities.TopWindow;
					window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
					window.Title = "Chibi Grf Utility";
					window.Icon = WpfUtilities.TopWindow.Icon;
					window.Background = (Brush)window.FindResource("TabItemBackground");
					window.Foreground = (Brush)window.FindResource("TextForeground");
					
					Grid grid = new Grid();
					window.Content = grid;
					
					grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });
					grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });
					grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });
					grid.RowDefinitions.Add(new RowDefinition());
					grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });
					
					grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });
					grid.ColumnDefinitions.Add(new ColumnDefinition());
					
					var pbDataGrfPath = new TokeiLibrary.WPF.Styles.PathBrowser() { MinWidth = 300, BrowseMode = TokeiLibrary.WPF.Styles.PathBrowser.BrowseModeType.File, Filter = "Grf Files|*.grf", UseSavePath = true, SavePathUniqueName = "ActEditor - ChibiGrf - SourceData - " };
					pbDataGrfPath.Text = @"C:\Games\RO\data.grf";
					pbDataGrfPath.SetValue(Grid.RowProperty, 0);
					pbDataGrfPath.SetValue(Grid.ColumnProperty, 1);
					pbDataGrfPath.Loaded += delegate {
						if (pbDataGrfPath.RecentFiles.Files.Count > 0)
							pbDataGrfPath.Text = pbDataGrfPath.RecentFiles.Files[0];
					};
					
					var pbOutputPath = new TokeiLibrary.WPF.Styles.PathBrowser() { MinWidth = 300, BrowseMode = TokeiLibrary.WPF.Styles.PathBrowser.BrowseModeType.File, Filter = "Grf Files|*.grf", UseSavePath = true, SavePathUniqueName = "ActEditor - ChibiGrf - DestData - " };
					pbOutputPath.Text = @"C:\Games\RO\ChibiGrf.grf";
					pbOutputPath.SetValue(Grid.RowProperty, 1);
					pbOutputPath.SetValue(Grid.ColumnProperty, 1);
					pbOutputPath.Loaded += delegate {
						if (pbOutputPath.RecentFiles.Files.Count > 0)
							pbOutputPath.Text = pbOutputPath.RecentFiles.Files[0];
					};
					
					var lab1 = new Label() { Padding = new Thickness(0), Margin = new Thickness(3), Content = "Source GRF", VerticalAlignment = VerticalAlignment.Center };
					var lab2 = new Label() { Padding = new Thickness(0), Margin = new Thickness(3), Content = "Output GRF", VerticalAlignment = VerticalAlignment.Center };
					var lab3 = new Label() { Padding = new Thickness(0), Margin = new Thickness(3), Content = "Scale", VerticalAlignment = VerticalAlignment.Center };
					lab2.SetValue(Grid.RowProperty, 1);
					lab3.SetValue(Grid.RowProperty, 2);
					
					var tbScale = new TextBox() { Margin = new Thickness(3), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
					Binder.Bind(tbScale, () => Configuration.ConfigAsker["[ActEditor - ChibiGrf - Scale]", "0.65"], v => Configuration.ConfigAsker["[ActEditor - ChibiGrf - Scale]"] = v.ToString(CultureInfo.InvariantCulture));
					tbScale.SetValue(Grid.ColumnProperty, 1);
					tbScale.SetValue(Grid.RowProperty, 2);
					
					var footer = new Grid() { Height = 40, Background = (Brush)window.FindResource("UIDialogBackground") };
					footer.SetValue(Grid.RowProperty, 4);
					footer.SetValue(Grid.ColumnSpanProperty, 2);
					
					var panel = new DockPanel() { HorizontalAlignment = HorizontalAlignment.Right };
					footer.Children.Add(panel);
					
					var btnOk = new Button() { Content = "Close", Height = 24, Width = 100, Margin = new Thickness(3) };
					btnOk.Click += delegate {
						window.Close();
					};
					
					var btnRun = new Button() { Content = "Run", Height = 24, Width = 100, Margin = new Thickness(3) };
					btnRun.Click += delegate {
						window.DialogResult = true;
						window.Close();
					};
					
					panel.Children.Add(btnRun);
					panel.Children.Add(btnOk);
					
					grid.Children.Add(lab1);
					grid.Children.Add(lab2);
					grid.Children.Add(lab3);
					grid.Children.Add(pbDataGrfPath);
					grid.Children.Add(pbOutputPath);
					grid.Children.Add(tbScale);
					grid.Children.Add(footer);
					
					window.Closed += delegate {
						window.Owner.Focus();
					};
					
					if (window.ShowDialog() == false)
						return;
					
					//=============================
					// End of Window creation
					//=============================
					
					var dataMaleHeadPath = @"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\³²\1_³²";
					var dataFemaleHeadPath = @"data\sprite\ÀÎ°£Á·\¸Ó¸®Åë\¿©\1_¿©";
					var dataGrfPath = pbDataGrfPath.Text;
					var grfDestinationPath = pbOutputPath.Text;
					var mag = FormatConverters.SingleConverter(Configuration.ConfigAsker["[ActEditor - ChibiGrf - Scale]", "0.65"]);
					
					// Used to resize weapons, shields, backpacks and wings.
					bool resizeAcc = true;	// Set this value to true to resize weapons, shields, backpacks and wings
					var foldersToExclude = new[] { @"\¸öÅë", @"\¸Ó¸®Åë" }.ToList();
					var foldersToRescale = new[] {
					    @"data\sprite\ÀÎ°£Á·",
					    @"data\sprite\·Îºê",
					    @"data\sprite\¹æÆÐ"
					};
					
					var exceptionRules = new[] {
						"¿î¿µÀÚ_³²",
						"ÃÊº¸ÀÚ_³²",
						"¸¶¹ý»ç_³²",
						"±â»ç_³²",
						"ÇÁ¸®½ºÆ®_³²",
						"¸ùÅ©_³²",
						"¼¼ÀÌÁö_³²",
						"¹Ùµå_³²",
						"±Ç¼ºÀ¶ÇÕ_³²",
						"±Ç¼º_³²",
						"¿ö·Ï_³²",
						"¾ÆÅ©ºñ¼ó_³²",
					};
					
					var exceptionOffsets = new [] {
						0, 3,
						-2, 0,
						-2, 0,
						2, 2,
						0, 3,
						-2, -2,
						0, 1,
						-2, 3,
						0, 2,
						-2, 1,
						-2, 3,
						-2, 3,
						-1, 2,
					};
					
					var progress = -1f;
					
					TaskManager.DisplayTaskC("Magnifying sprites...", "Please wait...", () => progress, isCancelling => {
					try {
						using (var grfDest = new GrfHolder(grfDestinationPath, GrfLoadOptions.New))
						using (var grf = new GrfHolder(dataGrfPath)) {
							var maleHeadReference = new Act(
								grf.FileTable[dataMaleHeadPath + ".act"].GetDecompressedData(), new Spr(
								grf.FileTable[dataMaleHeadPath + ".spr"].GetDecompressedData()));
						
							var femaleHeadReference = new Act(
								grf.FileTable[dataFemaleHeadPath + ".act"].GetDecompressedData(), new Spr(
								grf.FileTable[dataFemaleHeadPath + ".spr"].GetDecompressedData()));
						
							Action<FileEntry, Act> magnify = new Action<FileEntry, Act>((actEntry, actRef) => {
								if (!actEntry.RelativePath.IsExtension(".act")) return;
								if (isCancelling()) throw new OperationCanceledException();
								
								var sprEntry = grf.FileTable.TryGet(actEntry.RelativePath.ReplaceExtension(".spr"));
								var actBody = new Act(actEntry.GetDecompressedData(), sprEntry == null ? new Spr() : new Spr(sprEntry.GetDecompressedData()));
								
								var excpX = 0;
								var excpY = 0;
								var fileName = Path.GetFileNameWithoutExtension(actEntry.RelativePath);
								
								for (int k = 0; k < exceptionRules.Length; k++) {
									if (fileName == exceptionRules[k]) {
										excpX = - exceptionOffsets[2 * k + 0];
										excpY = exceptionOffsets[2 * k + 1];
										break;
									}
								}
								
								var mem = new MemoryStream();
								for (int ai = 0; ai < actBody.NumberOfActions; ai++) {
									for (int fi = 0; fi < actBody[ai].NumberOfFrames; fi++) {
										var frame = actBody.TryGetFrame(ai, fi);
										var frameRef = actRef.TryGetFrame(ai, fi);
					
										if (frame == null || frameRef == null) continue;
										if (frame.Anchors.Count == 0 || frameRef.Anchors.Count == 0) continue;
					
										var anchorFrame = frame.Anchors[0];
										var layerRef = frameRef.Layers[0];
										var anchorX = frame.Anchors[0].OffsetX;
										var anchorY = frame.Anchors[0].OffsetY;
										var diffX = anchorX - frameRef.Anchors[0].OffsetX - 1 + excpX;
										var diffY = anchorY - frameRef.Anchors[0].OffsetY - 2 + excpY;
										
										anchorFrame.OffsetX = (int) ((layerRef.OffsetX + diffX) * (mag - 1) + anchorX);
										anchorFrame.OffsetY = (int) ((layerRef.OffsetY + diffY) * (mag - 1) + anchorY);
									}
								}
					
								actBody.Magnify(mag);
								
								if (sprEntry == null) {
									actBody.SaveNoSprite(mem);
								}
								else {
									actBody.Save(mem);
								}
								
								grfDest.Commands.AddFile(actEntry.RelativePath, mem);
							});
							
							foreach (var entry in grf.FileTable.EntriesInDirectory(@"data\sprite\ÀÎ°£Á·\¸öÅë\³²\", SearchOption.AllDirectories)) magnify(entry, maleHeadReference);
							foreach (var entry in grf.FileTable.EntriesInDirectory(@"data\sprite\ÀÎ°£Á·\¸öÅë\¿©\", SearchOption.AllDirectories)) magnify(entry, femaleHeadReference);
							
							if (resizeAcc) {
								progress = 50f;
								var index = -2;
								var count = 0;
								var entries = new List<FileEntry>();
								
								foreach (var folderToRescale in foldersToRescale) {
									entries = entries.Concat(grf.FileTable.EntriesInDirectory(folderToRescale, SearchOption.AllDirectories)).ToList();
								}
								
								foreach (var actEntry in entries) {
									index++;
									
									if (!actEntry.RelativePath.IsExtension(".act")) continue;
									if (foldersToExclude.Any(p => actEntry.RelativePath.Contains(p))) continue;
									if (isCancelling()) throw new OperationCanceledException();
									
									var sprPath = actEntry.RelativePath.ReplaceExtension(".spr");
					                var sprEntry = grf.FileTable.TryGet(sprPath);
					                
					                var mem = new MemoryStream();
					                var spriteExists = sprEntry != null;
					                act = new Act(actEntry.GetDecompressedData(), spriteExists ? new Spr(sprEntry.GetDecompressedData()) : new Spr());
					                act.Magnify(mag, true); // Magnify the anchors as well
					
					                if (spriteExists) act.Save(mem);
					                else act.SaveNoSprite(mem);
					
					                grfDest.Commands.AddFile(actEntry.RelativePath, mem);
					                
					                progress = 50f + ((float) index / entries.Count) * 50f;
					                count++;
					                
					                if (count > 2000) {
										grfDest.Save();
										grfDest.Reload();
					                	count = 0;
					                }
								}
							}
							
							progress = -1;
							
							grfDest.Commands.End();
							grfDest.Save();
							grfDest.Reload();
							grfDest.Compact();
						}
					}
					catch (OperationCanceledException) { }
					catch (Exception err) {
					    ErrorHandler.HandleException(err);
					}
					finally {
						GC.Collect();
					    progress = 100f;
					}
					});
					}
					catch (Exception err) {
						backupErr = err;
					}
				}, "MyCustomScript", true);
				
				if (backupErr != null) {
					throw backupErr;
				}
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
	}
}
