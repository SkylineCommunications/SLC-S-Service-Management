namespace SLC_Service_Management.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	using Microsoft.Playwright;

	/// <summary>Installs Chromium if needed and keeps one headless browser for the whole test run.</summary>
	public sealed class BrowserFixture : IDisposable
	{
		private readonly IPlaywright playwright;

		private BrowserFixture(IPlaywright playwright, IBrowser browser)
		{
			this.playwright = playwright;
			Browser = browser;
		}

		public IBrowser Browser { get; }

		public static BrowserFixture Launch()
		{
			// Equivalent to 'playwright install chromium'; downloads into %LOCALAPPDATA%\ms-playwright when missing.
			int exitCode = Program.Main(new[] { "install", "chromium" });
			if (exitCode != 0)
			{
				throw new InvalidOperationException($"'playwright install chromium' exited with code {exitCode}. The QAOps DataMiner needs outbound access to the Playwright browser CDN.");
			}

			var playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
			try
			{
				var browser = playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).GetAwaiter().GetResult();
				return new BrowserFixture(playwright, browser);
			}
			catch
			{
				playwright.Dispose();
				throw;
			}
		}

		/// <summary>Creates an isolated context against the DataMiner web server, optionally signed in with a web session.</summary>
		public async Task<IBrowserContext> NewContextAsync(WebSessionFixture session)
		{
			var context = await Browser.NewContextAsync(new BrowserNewContextOptions
			{
				BaseURL = DmsTestSetup.WebBaseUrl,
				IgnoreHTTPSErrors = true,
				ServiceWorkers = ServiceWorkerPolicy.Block,
			});

			if (session != null)
			{
				await context.AddCookiesAsync(new List<Cookie>
				{
					new Cookie
					{
						Name = WebSessionFixture.CookieName,
						Value = session.ConnectionId,
						Url = DmsTestSetup.WebBaseUrl,
						SameSite = SameSiteAttribute.Strict,
					},
				});
			}

			return context;
		}

		public void Dispose()
		{
			try
			{
				Browser.CloseAsync().GetAwaiter().GetResult();
			}
			finally
			{
				playwright.Dispose();
			}
		}
	}
}
