namespace Skyline.DataMiner.Utils.ServiceManagement.Common.Tests.Extensions
{
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	[TestClass]
	public class StringExtensionsTests
	{
		[TestMethod]
		public void Wrap_EmptyLines_PreservesEmptyLines()
		{
			// Arrange
			string input = "First line\n\nSecond line";
			string expected = $"First line{Environment.NewLine}{Environment.NewLine}Second line";

			// Act
			var result = input.Wrap(20);

			// Assert
			Assert.AreEqual(expected, result);
		}

		[TestMethod]
		public void Wrap_LongWord_ExceedsLineWidth()
		{
			// Arrange
			string input = "Supercalifragilisticexpialidocious";
			string expected = "Supercalifragilisticexpialidocious";

			// Act
			var result = input.Wrap(10);

			// Assert
			Assert.AreEqual(expected, result);
		}

		[TestMethod]
		[DataRow(null, 10, null)]
		[DataRow("", 10, "")]
		[DataRow("Short", 10, "Short")]
		[DataRow("This is a test", 4, "This\r\nis a\r\ntest")]
		[DataRow("This is a test", 7, "This is\r\na test")]
		[DataRow("This is a test", 20, "This is a test")]
		[DataRow("This  is   a    test", 7, "This is\r\na test")]
		[DataRow("Line1\nLine2 is longer", 6, "Line1\r\nLine2\r\nis\r\nlonger")]
		[DataRow("Line1\r\nLine2 is longer", 6, "Line1\r\nLine2\r\nis\r\nlonger")]
		public void Wrap_VariousInputs_ExpectedOutput(string input, int width, string expected)
		{
			// Act
			if (expected != null)
			{
				expected = expected.Replace("\r\n", Environment.NewLine);
			}

			var result = input.Wrap(width);

			// Assert
			Assert.AreEqual(expected, result);
		}
	}
}