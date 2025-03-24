// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Dialogs;
using Microsoft.Agents.Builder.TestBot.Shared;

namespace Microsoft.BuilderSamples.Tests.Dialogs.TestData
{
    public class BookingDialogTestCase
    {
        /// <summary>
        /// Gets or sets the name for the test case.
        /// </summary>
        /// <value>The test case name.</value>
        public string Name { get; set; }

        public BookingDetails GetBookingDetailsDialogResult { get; set; }

        public string[,] UtterancesAndReplies { get; set; }

        public DialogTurnResult ExpectedDialogResult { get; set; }

        public bool BookedSuccessfully { get; set; }

        public bool FlightBookingServiceResult { get; set; }
    }
}
