namespace DiDemo;

public class FakeOrderRepository : IOrderRepository
{
    public List<Order> SavedOrders { get; } = new();

    public void Save(Order order) => SavedOrders.Add(order);
}
