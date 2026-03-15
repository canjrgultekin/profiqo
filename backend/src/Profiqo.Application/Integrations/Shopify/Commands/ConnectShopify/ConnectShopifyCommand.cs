// Path: backend/src/Profiqo.Application/Integrations/Shopify/Commands/ConnectShopify/ConnectShopifyCommand.cs
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Integrations.Shopify.Commands.ConnectShopify;

/// <summary>
/// Kullanıcı shopName + clientId + clientSecret girer.
/// Backend client_credentials grant ile token alır, credentials'ı encrypted DB'ye yazar.
/// </summary>
public sealed record ConnectShopifyCommand(
    string DisplayName,
    string ShopName,
    string ClientId,
    string ClientSecret
) : ICommand<Guid>;