﻿namespace OAuthServiceProvider.Code {
	using System;
	using System.Collections.Generic;
	using System.IdentityModel.Policy;
	using System.Linq;
	using System.Security.Principal;
	using System.ServiceModel;
	using System.ServiceModel.Channels;
	using System.ServiceModel.Security;
	using DotNetOpenAuth.OAuth;
	using DotNetOpenAuth.OAuth.ChannelElements;
	using DotNetOpenAuth.OAuth2;

	/// <summary>
	/// A WCF extension to authenticate incoming messages using OAuth.
	/// </summary>
	public class OAuthAuthorizationManager : ServiceAuthorizationManager {
		public OAuthAuthorizationManager() {
		}

		protected override bool CheckAccessCore(OperationContext operationContext) {
			if (!base.CheckAccessCore(operationContext)) {
				return false;
			}

			HttpRequestMessageProperty httpDetails = operationContext.RequestContext.RequestMessage.Properties[HttpRequestMessageProperty.Name] as HttpRequestMessageProperty;
			Uri requestUri = operationContext.RequestContext.RequestMessage.Properties["OriginalHttpRequestUri"] as Uri;

			try {
				var principal = this.VerifyOAuth2(httpDetails, requestUri);
				if (principal != null) {
					var policy = new OAuthPrincipalAuthorizationPolicy(principal);
					var policies = new List<IAuthorizationPolicy> {
						policy,
					};

					var securityContext = new ServiceSecurityContext(policies.AsReadOnly());
					if (operationContext.IncomingMessageProperties.Security != null) {
						operationContext.IncomingMessageProperties.Security.ServiceSecurityContext = securityContext;
					} else {
						operationContext.IncomingMessageProperties.Security = new SecurityMessageProperty {
							ServiceSecurityContext = securityContext,
						};
					}

					securityContext.AuthorizationContext.Properties["Identities"] = new List<IIdentity> {
						principal.Identity,
					};

					// Only allow this method call if the access token scope permits it.
					return principal.IsInRole(operationContext.IncomingMessageHeaders.Action);
				} else {
					return false;
				}
			} catch (ProtocolException ex) {
				Global.Logger.Error("Error processing OAuth messages.", ex);
			}

			return false;
		}

		private OAuthPrincipal VerifyOAuth1(HttpRequestMessageProperty httpDetails, Uri requestUri) {
			ServiceProvider sp = Constants.CreateServiceProvider();
			var auth = sp.ReadProtectedResourceAuthorization(httpDetails, requestUri);
			if (auth != null) {
				var accessToken = Global.DataContext.OAuthTokens.Single(token => token.Token == auth.AccessToken);
				var principal = sp.CreatePrincipal(auth);
				return principal;
			}

			return null;
		}

		private OAuthPrincipal VerifyOAuth2(HttpRequestMessageProperty httpDetails, Uri requestUri) {
			// for this sample where the auth server and resource server are the same site,
			// we use the same public/private key.
			var resourceServer = new ResourceServer(
				new StandardAccessTokenAnalyzer(
					OAuth2AuthorizationServer.AsymmetricKey,
					OAuth2AuthorizationServer.AsymmetricKey));

			string username, scope;
			var error = resourceServer.VerifyAccess(new DotNetOpenAuth.Messaging.HttpRequestInfo(httpDetails, requestUri), out username, out scope);
			if (error == null) {
				string[] scopes = scope.Split(new char[] { ' ' });
				var principal = new OAuthPrincipal(username, scopes);
				return principal;
			} else {
				return null;
			}
		}
	}
}