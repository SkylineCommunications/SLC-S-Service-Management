namespace SLC_Service_Management.IntegrationTests
{
	using System;
	using System.Threading.Tasks;

	using Microsoft.Playwright;
	using Microsoft.VisualStudio.TestTools.UnitTesting;

	/// <summary>
	/// Read-only checks of the deployed Service Management web app
	/// (port of <c>Service-Management-App/e2e-live/app.live.spec.ts</c>). Navigates without creating or changing data.
	/// </summary>
	[TestClass]
	[TestCategory("IntegrationTest")]
	[TestCategory("ServiceManagementApp")]
	public class ServiceManagementAppTests
	{
		/// <summary>Matches <c>Constants.SolutionAppUrl</c> in the solution installer and the Vite <c>outDir</c>.</summary>
		private const string AppRootUrl = "/public/service-management/index.html";

		private static readonly TimeSpan ExpectTimeout = TimeSpan.FromSeconds(20);

		private IBrowserContext context;
		private IPage page;

		[TestCleanup]
		public async Task TestCleanup()
		{
			if (context != null)
			{
				await context.CloseAsync();
				context = null;
				page = null;
			}
		}

		[TestMethod]
		public async Task WithoutSession_RedirectsToDataMinerAuthentication()
		{
			await OpenPageAsync(signedIn: false, hash: "#orders");

			await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("^https://localhost/auth/"), new PageAssertionsToHaveURLOptions { Timeout = (float)ExpectTimeout.TotalMilliseconds });
		}

		[TestMethod]
		public async Task SignedIn_OpensOrdersPage()
		{
			await OpenPageAsync(signedIn: true, hash: "#orders");

			await ExpectHeadingAsync("Orders");
			Assert.AreEqual(AppRootUrl, new Uri(page.Url).AbsolutePath, Describe("The app left its root URL."));
		}

		[TestMethod]
		public async Task SignedIn_NavigatesBetweenWorkspacePages()
		{
			await OpenPageAsync(signedIn: true, hash: "#orders");
			await ExpectHeadingAsync("Orders");

			var navigation = page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Primary navigation" });
			foreach (string name in new[] { "Services", "Catalog", "Settings" })
			{
				await navigation.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = name }).ClickAsync();
				await ExpectHeadingAsync(name);
			}
		}

		[TestMethod]
		public async Task SignedIn_LoadsOverviewFromLiveData()
		{
			await OpenPageAsync(signedIn: true, hash: null);

			var overviewLoaded = page.WaitForResponseAsync(
				response => response.Url.Contains("/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput") && response.Ok,
				new PageWaitForResponseOptions { Timeout = (float)ExpectTimeout.TotalMilliseconds });

			await page.GotoAsync(AppRootUrl + "#overview");
			await ExpectHeadingAsync("Service Management");
			await overviewLoaded;

			await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = (float)ExpectTimeout.TotalMilliseconds });
		}

		private async Task OpenPageAsync(bool signedIn, string hash)
		{
			context = await DmsTestSetup.Browser.NewContextAsync(signedIn ? DmsTestSetup.WebSession : null);
			page = await context.NewPageAsync();

			if (hash != null)
			{
				await page.GotoAsync(AppRootUrl + hash);
			}
		}

		private async Task ExpectHeadingAsync(string name)
		{
			var heading = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = name, Exact = true });
			try
			{
				await Assertions.Expect(heading).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = (float)ExpectTimeout.TotalMilliseconds });
			}
			catch (PlaywrightException ex)
			{
				Assert.Fail(Describe($"Heading '{name}' did not become visible: {ex.Message}"));
			}
		}

		private string Describe(string message)
		{
			string bodyText;
			try
			{
				bodyText = page.InnerTextAsync("body", new PageInnerTextOptions { Timeout = 2000 }).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				bodyText = "(body text unavailable: " + ex.Message + ")";
			}

			return $"{message} URL: {page.Url}. Page text: {ApiResponse.Truncate(bodyText)}";
		}
	}
}
