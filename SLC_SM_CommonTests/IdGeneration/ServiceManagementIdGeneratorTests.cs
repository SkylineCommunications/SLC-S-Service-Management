// <copyright file="ServiceManagementIdGeneratorTests.cs" company="Skyline Communications">
// Copyright (c) Skyline Communications. All rights reserved.
// </copyright>

namespace SLC_SM_CommonTests.IdGeneration
{
	using System;
	using SLC_SM_Common.IdGeneration;

	[TestClass]
	public class ServiceManagementIdGeneratorTests
	{
		[TestMethod]
		public void MissingOrResetCounter_SeedsFirstReservationAboveExistingMaximum()
		{
			foreach (long? next in new long?[] { null, 0, 1 })
			{
				Assert.AreEqual(101L, ServiceManagementIdGenerator.GetInitialReservationFloor(next, () => 100));
			}
		}

		[TestMethod]
		public void ResetCounterWithoutRecords_StartsAtOne()
		{
			Assert.AreEqual(1L, ServiceManagementIdGenerator.GetInitialReservationFloor(1, () => 0));
		}

		[TestMethod]
		public void InitializedCounter_DoesNotScanExistingRecords()
		{
			Assert.AreEqual(1L, ServiceManagementIdGenerator.GetInitialReservationFloor(101, () => throw new InvalidOperationException("Unexpected scan.")));
		}

		[TestMethod]
		public void ResetCounterWithMaximumIntegerSuffix_UsesLongFloor()
		{
			Assert.AreEqual(2147483648L, ServiceManagementIdGenerator.GetInitialReservationFloor(1, () => int.MaxValue));
		}

		[DataTestMethod]
		[DataRow("SERVICE-")]
		[DataRow("ORDER-")]
		public void LargeExistingSuffix_IsPreservedWhenSeedingCounter(string prefix)
		{
			string[] ids = { prefix + "00001", prefix + "3000000000", prefix + "2147483647" };
			long highestSuffix = ServiceManagementIdGenerator.HighestSuffix(ids, prefix);

			Assert.AreEqual(3000000000L, highestSuffix);
			Assert.AreEqual(3000000001L, ServiceManagementIdGenerator.GetInitialReservationFloor(null, () => highestSuffix));
			Assert.AreEqual(3000000001L, ServiceManagementIdGenerator.GetInitialReservationFloor(1, () => highestSuffix));
		}

		[DataTestMethod]
		[DataRow("SERVICE-")]
		[DataRow("ORDER-")]
		public void MaximumLongSuffix_IsPreservedAndCannotWrapToNextNumber(string prefix)
		{
			long highestSuffix = ServiceManagementIdGenerator.HighestSuffix(new[] { prefix + "9223372036854775807" }, prefix);

			Assert.AreEqual(long.MaxValue, highestSuffix);
			Assert.ThrowsException<OverflowException>(() => ServiceManagementIdGenerator.GetInitialReservationFloor(null, () => highestSuffix));
			Assert.ThrowsException<OverflowException>(() => ServiceManagementIdGenerator.GetInitialReservationFloor(1, () => highestSuffix));
		}

		[TestMethod]
		public void LastRepresentableNextNumber_DoesNotOverflow()
		{
			long highestSuffix = ServiceManagementIdGenerator.HighestSuffix(new[] { "SERVICE-9223372036854775806" }, "SERVICE-");

			Assert.AreEqual(long.MaxValue, ServiceManagementIdGenerator.GetInitialReservationFloor(1, () => highestSuffix));
		}

		[TestMethod]
		public void HighestSuffix_IgnoresMalformedAndUnrelatedIds()
		{
			string[] ids = { null!, string.Empty, " ", "ORDER-999", "SERVICE-", "SERVICE--1", "SERVICE-1.5", "SERVICE-text", "service-00042" };

			Assert.AreEqual(42L, ServiceManagementIdGenerator.HighestSuffix(ids, "SERVICE-"));
			Assert.AreEqual(0L, ServiceManagementIdGenerator.HighestSuffix(Array.Empty<string>(), "SERVICE-"));
		}
	}
}
