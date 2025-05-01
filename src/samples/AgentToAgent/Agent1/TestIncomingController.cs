// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Extensions.Logging;

namespace Agent1;

// ASP.Net Controller that receives incoming HTTP requests from the Azure Bot Service or other configured event activity protocol sources.
// When called, the request has already been authorized and credentials and tokens validated.
[AllowAnonymous]
[ApiController]
[Route("api/test")]
public class TestIncomingController(ILogger logger) : ChannelApiController(new TestApiHandler(logger))
{
}