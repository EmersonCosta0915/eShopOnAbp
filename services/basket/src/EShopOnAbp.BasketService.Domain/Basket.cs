using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities;

namespace EShopOnAbp.BasketService;

public class Basket : AggregateRoot<Guid>
{
    public List<BasketItem> Items { get; set; }

    private Basket()
    {
        
    }
    
    public Basket(Guid id) 
        : base(id)
    {
        Items = new List<BasketItem>();
    }

    public void AddProduct(Guid productId, int count = 1)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Product count should be 1 or more!");
        }

        var item = Items.FirstOrDefault(x => x.ProductId == productId);
        if (item == null)
        {
            Items.Add(new BasketItem(productId, count));
        }
        else
        {
            item.Count += count;
        }
    }
    
    public void RemoveProduct(Guid productId, int? count = null)
    {
        if (count is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Product count should be null, 1 or more!");
        }
        
        var item = Items.FirstOrDefault(x => x.ProductId == productId);
        if (item == null)
        {
            return;
        }
        
        if (count == null || item.Count <= count)
        {
            Items.Remove(item);
            return;
        }

        item.Count -= count.Value;
    }
}