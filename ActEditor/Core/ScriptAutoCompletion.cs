using ActEditor.Core.Scripting;
using ActEditor.Core.WPF.Dialogs;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TokeiLibrary;
using Utilities;

namespace ActEditor.Core {
	public class ScriptAutoCompletion {
		private TextEditor _textEditor;

		private int _completionStartOffset;
		private CompletionWindow _completionWindow;
		private List<ICompletionData> _data;
		private int _startCursorFixAdjust;

		public ScriptAutoCompletion(TextEditor textEditor) {
			_textEditor = textEditor;
			_startCursorFixAdjust = ScriptRunnerDialog.ScriptTemplate.Replace("{0}", ScriptRunnerDialog.DefaultScriptName).Replace("{1}", ScriptRunnerDialog.DefaultInputGesture).IndexOf("{3}");
		}

		internal void ProcessText(TextCompositionEventArgs e) {
			if (e.Text.Length == 1) {
				switch (e.Text) {
					case ".":
						ShowCompletion();
						break;
					case " ":
					case ",":
					case "{":
					case "}":
					case "[":
					case "]":
					case "(":
					case ")":
						FilterCheck(e, false);
						break;
					case "\t":
						FilterCheck(e, true);
						break;
				}
			}

			if (!string.IsNullOrEmpty(e.Text) && char.IsLetterOrDigit(e.Text[0])) {
				UpdateFilter();
			}
		}

		internal void UpdateFilter() {
			_textEditor.Dispatcher.BeginInvoke((System.Action)_updateFilter);
		}

		private void _updateFilter() {
			if (_completionWindow == null)
				return;

			string filter = GetCurrentFilter();

			_completionWindow.CompletionList.SelectItem(filter);

			if (_completionWindow.CompletionList.ListBox.Items.Count == 0) {
				_completionWindow.Visibility = Visibility.Hidden;
				
				if (_completionWindow.ToolTipCompletion != null)
					_completionWindow.ToolTipCompletion.Visibility = Visibility.Hidden;
			}
			else {
				_completionWindow.Visibility = Visibility.Visible;

				if (_completionWindow.ToolTipCompletion != null)
					_completionWindow.ToolTipCompletion.Visibility = Visibility.Visible;
			}
		}

		private void FilterCheck(TextCompositionEventArgs e, bool eatKey) {
			if (_completionWindow != null) {
				if (_completionWindow.CompletionList.SelectedItem is ICompletionData item) {
					if (!eatKey && e != null)
						((MyCompletionData)item).AddKey(e.Text);

					item.Complete(
						_textEditor.TextArea,
						new SimpleSegment(_completionWindow.StartOffset, _completionWindow.EndOffset - _completionWindow.StartOffset),
						new EventArgs());
				}

				_completionWindow.Close();

				if (eatKey && e != null)
					e.Handled = true;

				return;
			}

			return;
		}

		private bool _compiling = false;

		public void ShowCompletion() {
			if (_compiling)
				return;

			_compiling = true;

			string script = ScriptRunnerDialog.ScriptTemplate.Replace("{0}", ScriptRunnerDialog.DefaultScriptName).Replace("{1}", ScriptRunnerDialog.DefaultInputGesture).Replace("{2}", ScriptRunnerDialog.DefaultImage).Replace("{3}", _textEditor.Text);
			int roslynPosition = _startCursorFixAdjust + _textEditor.CaretOffset;
			script = script.Substring(0, roslynPosition + 1);

			// This needs to be done on a thread
			Task.Run(() => {
				try {
					var syntaxTree = CSharpSyntaxTree.ParseText(script);

					var compilation = CSharpCompilation.Create(
						"DynamicAssembly",
						new[] { syntaxTree },
						ScriptLoader.GetReferences(),
						new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

					var semanticModel = compilation.GetSemanticModel(syntaxTree, false);
					var root = semanticModel.SyntaxTree.GetRoot();
					var token = root.FindToken(roslynPosition - 1);
					var memberAccess = token.Parent.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();

					ITypeSymbol type = null;

					if (memberAccess != null) {
						// instance type
						var expression = memberAccess.Expression;

						var symbolInfo = semanticModel.GetSymbolInfo(expression);
						var symbol = symbolInfo.Symbol;

						bool isStaticAccess = symbol?.Kind == SymbolKind.NamedType;
						type = semanticModel.GetTypeInfo(memberAccess).Type;

						if (type == null || type.Name == "") {
							type = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
						}

						if (type != null) {
							List<ISymbol> members = new List<ISymbol>();

							foreach (var member in type.GetMembers()) {
								if (member.DeclaredAccessibility != Accessibility.Public)
									continue;

								if (isStaticAccess) {
									if (!member.IsStatic)
										continue;
								}
								else {
									if (member.IsStatic)
										continue;
								}

								switch (member.Kind) {
									case SymbolKind.Event:
										members.Add(member);
										break;
									case SymbolKind.Method:
										var methodSymbol = (IMethodSymbol)member;

										if (methodSymbol.MethodKind == MethodKind.Constructor)
											continue;

										if (methodSymbol.MethodKind != MethodKind.Ordinary)
											continue;

										members.Add(member);
										break;
									case SymbolKind.Property:
									case SymbolKind.Field:
										members.Add(member);
										break;
									default:
										continue;
								}
							}

							if (members.Count > 0) {
								_textEditor.Dispatch(delegate {
									_completionStartOffset = _textEditor.CaretOffset;
									_completionWindow = new CompletionWindow(_textEditor.TextArea);
									_completionWindow.AllowsTransparency = true;

									_data = new List<ICompletionData>();

									var groups = members.GroupBy(p => p.Name).ToArray();

									foreach (var symbolS in groups.OrderBy(p => p.First().Name)) {
										//data.Add(new RoslynCompletionData(symbolS));
										if (symbolS.Count() == 1)
											_data.Add(new MyCompletionData(symbolS.First()));
										else
											_data.Add(new MyCompletionData(symbolS.ToList()));
									}

									foreach (var data in _data) {
										_completionWindow.CompletionList.CompletionData.Add(data);
									}

									_completionWindow.Show();
									_completionWindow.Closed += delegate {
										_completionWindow = null;
									};
								});
							}
						}
					}
				}
				catch { }
				finally { 
					_compiling = false;
				}
			});
		}

		public string GetCurrentFilter() {
			int caret = _textEditor.CaretOffset;
			if (caret < _completionStartOffset)
				return "";

			return _textEditor.Document.GetText(
				_completionStartOffset,
				caret - _completionStartOffset);
		}

		internal void CloseWindow() {
			if (_completionWindow != null)
				_completionWindow.Close();
		}
	}

	public class MyCompletionData : ICompletionData {
		public ImageSource Image => GetImageSource();

		private ImageSource GetImageSource() {
			if (_symbol is IPropertySymbol) {
				return ApplicationManager.PreloadResourceImage("properties.png");
			}

			if (_symbol is IMethodSymbol) {
				return ApplicationManager.PreloadResourceImage("diff.png");
			}

			if (_symbol is IFieldSymbol) {
				return ApplicationManager.PreloadResourceImage("field.png");
			}

			if (_symbol is IEventSymbol) {
				return ApplicationManager.PreloadResourceImage("event.png");
			}

			return null;
		}

		private ISymbol _symbol;
		private List<ISymbol> _symbols;
		private string _toAdd;

		public string Text { get; }

		public object Content => GetShortSignature();

		public object Description => GetDescription();

		public double Priority => 0;

		public MyCompletionData(ISymbol symbol) {
			_symbol = symbol;

			_symbols = new List<ISymbol>();
			_symbols.Add(_symbol);

			Text = GetName();
		}

		public MyCompletionData(List<ISymbol> symbols) {
			_symbol = symbols.First();
			_symbols = symbols;

			Text = GetName();
		}

		private string GetName() {
			return _symbol.Name;
		}

		public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) {
			textArea.Document.Replace(completionSegment, Text + _toAdd ?? "");
		}

		private object GetShortSignature() {
			return Text;
		}

		static readonly SymbolDisplayFormat IntelliSenseFormat = new SymbolDisplayFormat(
			globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
			parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName,
			propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
		);

		private string GetSignature(ISymbol symbol) {
			if (symbol is IMethodSymbol method) {
				return method.ToDisplayString(IntelliSenseFormat);
			}

			if (symbol is IPropertySymbol property) {
				return property.ToDisplayString(IntelliSenseFormat);
			}

			if (symbol is IFieldSymbol field) {
				return field.ToDisplayString(IntelliSenseFormat);
			}

			if (symbol is IEventSymbol evt) {
				return evt.ToDisplayString(IntelliSenseFormat);
			}

			if (symbol is IArrayTypeSymbol array) {
				return array.ToDisplayString(IntelliSenseFormat);
			}

			return symbol.Name;
		}

		private string GetSignature() {
			return Methods.Aggregate(_symbols.Select(p => GetSignature(p)).ToList(), "\r\n");
		}

		private string GetDescription() {
			var signature = GetSignature();
			//var xml = _symbol.GetDocumentationCommentXml();
			//
			//if (!string.IsNullOrWhiteSpace(xml)) {
			//	signature += "\r\n" + ParseXmlComments(xml);
			//}

			return signature;
		}

		internal void AddKey(string text) {
			_toAdd = text;
		}

		public string ParseXmlComments(string text) {
			return text;
		}
	}
}
