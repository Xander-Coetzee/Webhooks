foreach (var webhook in pendingWebhooks)
{
    // 1. Fetch details from Acme API
    var acmeOrder = await _acmeService.GetOrderDetailsAsync(webhook.ExternalOrderId);

    // 2. Map to our database Order model
    var newOrder = new Order
    {
        SourceSystem = "Acme", // Here is our label!
        ExternalOrderId = acmeOrder.ExternalOrderId,
        OrderNumber = acmeOrder.OrderNumber,
        OrderTotal = acmeOrder.OrderTotal,
        OrderDate = DateTimeOffset.UtcNow,
        Status = "New",
    };

    // 3. Map the items (Lines)
    foreach (var line in acmeOrder.Lines)
    {
        newOrder.Lines.Add(
            new OrderLine
            {
                Sku = line.Sku,
                Qty = line.Qty,
                UnitPrice = line.UnitPrice,
            }
        );
    }

    // 4. Save to DB and update Webhook status
    context.Orders.Add(newOrder);
    webhook.Status = "Processed";

    await context.SaveChangesAsync();
}
