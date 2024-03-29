﻿using IdentityModel.AspNetCore.AccessTokenManagement;
using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace GloboTicket.Services.ShoppingBasket.Helpers
{
    public class TokenExchangeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IClientAccessTokenCache _clientAccessTokenCache;


        public TokenExchangeService(IHttpClientFactory httpClientFactory,
            IClientAccessTokenCache clientAccessTokenCache)
        {
            _clientAccessTokenCache = clientAccessTokenCache;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> GetTokenAsync(string incomingToken, string apiScope)
        {
            var param = new ClientAccessTokenParameters()
            {
                ForceRenewal = true,
            };

            var item = await _clientAccessTokenCache
                .GetAsync($"shoppingbaskettodownstreamtokenexchangeclient_{apiScope}", param);
            if (item != null)
            {
                return item.AccessToken;
            }

            var client = _httpClientFactory.CreateClient();

            var discoveryDocumentResponse = await client
                .GetDiscoveryDocumentAsync("https://localhost:5010/");
            if (discoveryDocumentResponse.IsError)
            {
                throw new Exception(discoveryDocumentResponse.Error);
            }

            var customParams = new Parameters(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("subject_token_type", "urn:ietf:params:oauth:token-type:access_token"),
                new KeyValuePair<string, string>("subject_token", incomingToken),
                new KeyValuePair<string, string>("scope", $"openid profile {apiScope}")
            });

            var tokenResponse = await client.RequestTokenAsync(new TokenRequest()
            {
                Address = discoveryDocumentResponse.TokenEndpoint,
                GrantType = "urn:ietf:params:oauth:grant-type:token-exchange",
                Parameters = customParams,
                ClientId = "shoppingbaskettodownstreamtokenexchangeclient",
                ClientSecret = "0cdea0bc-779e-4368-b46b-09956f70712c"
            });

            if (tokenResponse.IsError)
            {
                throw new Exception(tokenResponse.Error);
            }

            await _clientAccessTokenCache.SetAsync(
                $"shoppingbaskettodownstreamtokenexchangeclient_{apiScope}",
                tokenResponse.AccessToken,
                tokenResponse.ExpiresIn, param);

            return tokenResponse.AccessToken;
        }
    }
}
