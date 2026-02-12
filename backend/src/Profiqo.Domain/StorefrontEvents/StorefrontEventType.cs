namespace Profiqo.Domain.StorefrontEvents;

public enum StorefrontEventType
{
    AddToCart = 1,
    RemoveFromCart = 2,
    CompleteCheckout = 3,
    AddToWishlist = 4,
    PageView = 5,
    ProductView = 6,
    BeginCheckout = 7,
    Search = 8
}
