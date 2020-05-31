﻿using System;
using Xunit;

namespace YY.EventLogReaderAssistant.Services.Tests
{
    public class IntExtensionsTests
    {
        #region Public Methods

        [Fact]
        public void ToDateTimeFormat_Test()
        {
            long sourceLong = 637149888000000;
            DateTime checkDate = new DateTime(2020, 1, 19);
            DateTime? resultDate = sourceLong.ToDateTimeFormat();

            Assert.Equal(checkDate, resultDate);
        }

        [Fact]
        public void ToNullableDateTimeELFormat_Test()
        {
            long sourceLong = 637149888000000;
            DateTime checkDate = new DateTime(2020, 1, 19);
            DateTime? resultDate = sourceLong.ToNullableDateTimeElFormat();

            Assert.Equal(checkDate, resultDate);
        }

        #endregion
    }
}
