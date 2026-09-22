namespace DiDemo;

public class SqlOrderRepository : IOrderRepository
{
    private readonly string _connectionString;

    public SqlOrderRepository(string connectionString) => _connectionString = connectionString;

    public void Save(Order order) =>
        Console.WriteLine($"  [SqlOrderRepository via '{_connectionString}'] saved order #{order.Id}");
}
