using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorManager;

namespace ActEditor.Core {
	public sealed class Debouncer {
		private readonly object _lock = new object();
		private CancellationTokenSource _cts;

		public void Execute(Action action, int delayMs = 0) {
			lock (_lock) {
				_cts?.Cancel();
				_cts = new CancellationTokenSource();
				var token = _cts.Token;

				Task.Run(async () => {
					try {
						if (delayMs > 0)
							await Task.Delay(delayMs, token);

						if (!token.IsCancellationRequested)
							action();
					}
					catch (OperationCanceledException) { }
					catch (Exception ex) {
						ErrorHandler.HandleException(ex);
					}
				});
			}
		}
	}

	public sealed class CoalescingExecutor {
		private int _running;
		private int _pending;

		public void Execute(Action action) {
			if (Interlocked.Exchange(ref _pending, 1) == 1) {
				return;
			}

			Task.Run(() => {
				if (Interlocked.Exchange(ref _running, 1) == 1)
					return;

				try {
					do {
						Interlocked.Exchange(ref _pending, 0);
						action();
					}
					while (Interlocked.Exchange(ref _pending, 0) == 1);
				}
				finally {
					Interlocked.Exchange(ref _running, 0);
				}
			});
		}
	}
}
