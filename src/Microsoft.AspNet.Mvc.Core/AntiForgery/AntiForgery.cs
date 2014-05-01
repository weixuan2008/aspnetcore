// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Security.DataProtection;

namespace Microsoft.AspNet.Mvc
{
    /// <summary>
    /// Provides access to the anti-forgery system, which provides protection against
    /// Cross-site Request Forgery (XSRF, also called CSRF) attacks.
    /// </summary>
    public sealed class AntiForgery
    {
        private static readonly string _purpose = "Microsoft.AspNet.Mvc.AntiXsrf.AntiForgeryToken.v1";
        private readonly AntiForgeryWorker _worker;

        public AntiForgery([NotNull] IClaimUidExtractor claimUidExtractor,
                           [NotNull] IDataProtectionProvider dataProtectionProvider,
                           [NotNull] IAntiForgeryAdditionalDataProvider additionalDataProvider)
        {
            // TODO: This is temporary till we figure out how to flow configs using DI.
            var config = new AntiForgeryConfigWrapper();
            var serializer = new AntiForgeryTokenSerializer(dataProtectionProvider.CreateProtector(_purpose));
            var tokenStore = new AntiForgeryTokenStore(config, serializer);
            var tokenProvider = new TokenProvider(config, claimUidExtractor, additionalDataProvider);
            _worker = new AntiForgeryWorker(serializer, config, tokenStore, tokenProvider, tokenProvider);
        }

        /// <summary>
        /// Generates an anti-forgery token for this request. This token can
        /// be validated by calling the Validate() method.
        /// </summary>
        /// <param name="context">The HTTP context associated with the current call.</param>
        /// <returns>An HTML string corresponding to an &lt;input type="hidden"&gt;
        /// element. This element should be put inside a &lt;form&gt;.</returns>
        /// <remarks>
        /// This method has a side effect:
        /// A response cookie is set if there is no valid cookie associated with the request.
        /// </remarks>
        public HtmlString GetHtml([NotNull] HttpContext context)
        {
            var builder = _worker.GetFormInputElement(context);
            return builder.ToHtmlString(TagRenderMode.SelfClosing);
        }

        /// <summary>
        /// Generates an anti-forgery token pair (cookie and form token) for this request.
        /// This method is similar to GetHtml(HttpContext context), but this method gives the caller control
        /// over how to persist the returned values. To validate these tokens, call the
        /// appropriate overload of Validate.
        /// </summary>
        /// <param name="context">The HTTP context associated with the current call.</param>
        /// <param name="oldCookieToken">The anti-forgery token - if any - that already existed
        /// for this request. May be null. The anti-forgery system will try to reuse this cookie
        /// value when generating a matching form token.</param>
        /// <remarks>
        /// Unlike the GetHtml(HttpContext context) method, this method has no side effect. The caller
        /// is responsible for setting the response cookie and injecting the returned
        /// form token as appropriate.
        /// </remarks>
        public AntiForgeryTokenSet GetTokens([NotNull] HttpContext context, string oldCookieToken)
        {
            // Will contain a new cookie value if the old cookie token
            // was null or invalid. If this value is non-null when the method completes, the caller
            // must persist this value in the form of a response cookie, and the existing cookie value
            // should be discarded. If this value is null when the method completes, the existing
            // cookie value was valid and needn't be modified.
            return _worker.GetTokens(context, oldCookieToken);
        }

        /// <summary>
        /// Validates an anti-forgery token that was supplied for this request.
        /// The anti-forgery token may be generated by calling GetHtml(HttpContext context).
        /// </summary>
        /// <param name="context">The HTTP context associated with the current call.</param>
        public async Task ValidateAsync([NotNull] HttpContext context)
        {
           await _worker.ValidateAsync(context);
        }

        /// <summary>
        /// Validates an anti-forgery token pair that was generated by the GetTokens method.
        /// </summary>
        /// <param name="context">The HTTP context associated with the current call.</param>
        /// <param name="cookieToken">The token that was supplied in the request cookie.</param>
        /// <param name="formToken">The token that was supplied in the request form body.</param>
        public void Validate([NotNull] HttpContext context, string cookieToken, string formToken)
        {
            _worker.Validate(context, cookieToken, formToken);
        }

        public void Validate([NotNull] HttpContext context, AntiForgeryTokenSet antiForgeryTokenSet)
        {
            Validate(context, antiForgeryTokenSet.CookieToken, antiForgeryTokenSet.FormToken);
        }
    }
}