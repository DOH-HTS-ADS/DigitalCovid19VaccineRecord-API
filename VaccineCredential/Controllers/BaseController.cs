﻿using Application.Common.Models;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace VaccineCredential.Api.Controllers
{
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        private IMediator _mediator;
        private IMapper _mapper;

        protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetService<IMediator>();
        protected IMapper Mapper => _mapper ??= HttpContext.RequestServices.GetService<IMapper>();

        protected StatusCodeResult HandleRateLimit(RateLimit rateLimit)
        {
            StatusCodeResult scr = null;
            if (rateLimit.Limit < 0)
            {
                return scr;
            }
            Response.Headers["X-RateLimit-Limit"] = rateLimit.Limit.ToString();
            Response.Headers["X-RateLimit-Remaining"] = rateLimit.Remaining.ToString();
            Response.Headers["X-RateLimit-Reset"] = rateLimit.TimeRemaining.ToString();
            if (rateLimit.Remaining < 0)
            {
                scr = StatusCode(429);
            }
            return scr;
        }
    }
}