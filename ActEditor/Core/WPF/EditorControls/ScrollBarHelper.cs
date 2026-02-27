using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TokeiLibrary;

namespace ActEditor.Core.WPF.EditorControls {
	public static class ScrollBarHelper {
		public static void OverrideMouseIncrement(ScrollBar bar, Action increment, Action decrement) {
			long lastClicked = DateTime.Now.Ticks;

			bar.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) {
				if (bar.Track.IsMouseOver) {
					return;
				}

				Point mousePosition = e.GetPosition(bar);

				Rect leftButtonArea = new Rect(0, 0, SystemParameters.HorizontalScrollBarButtonWidth, bar.ActualHeight);
				Rect rightButtonArea = new Rect(bar.ActualWidth - SystemParameters.HorizontalScrollBarButtonWidth, 0, SystemParameters.HorizontalScrollBarButtonWidth, bar.ActualHeight);

				bool isLeft = leftButtonArea.Contains(mousePosition);
				bool isRight = rightButtonArea.Contains(mousePosition);

				lastClicked = DateTime.Now.Ticks;

				Task.Run(() => {
					bool firstClick = true;
					while (bar.Dispatch(() => Mouse.LeftButton) == MouseButtonState.Pressed) {
						bar.Dispatch(delegate {
							mousePosition = e.GetPosition(bar);

							isLeft = leftButtonArea.Contains(mousePosition);
							isRight = rightButtonArea.Contains(mousePosition);
						});

						if (isLeft)
							decrement();
						else if (isRight)
							increment();

						var beforeSleep = DateTime.Now.Ticks;

						Thread.Sleep(firstClick ? 400 : 50);
						
						if (lastClicked > beforeSleep) {
							Console.WriteLine("Cancelled thread");
							break;
						}

						firstClick = false;
					}
				});

				e.Handled = true;
			};
		}
	}
}
