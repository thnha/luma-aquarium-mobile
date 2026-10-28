using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Lumaquarium.Shop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Purchasing;

public sealed class IAPManagerTests
{
    private GameObject owner;
    private IAPManager manager;
    private TestProducts products;

    [SetUp]
    public void SetUp()
    {
        owner = new GameObject("IAP test");
        manager = owner.AddComponent<IAPManager>();
        products = new TestProducts();
        // Avoid starting a platform store in EditMode; inject only its product boundary.
        var store = (StoreController)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(StoreController));
        typeof(StoreController).GetField("m_ProductService", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(store, products);
        typeof(IAPManager).GetField("store", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, store);
        typeof(IAPManager).GetProperty("Connected").SetValue(manager, true);
        products.OnProductsFetched += (Action<List<Product>>)Delegate.CreateDelegate(
            typeof(Action<List<Product>>), manager, "OnProductsFetched");
        products.OnProductsFetchFailed += (Action<ProductFetchFailed>)Delegate.CreateDelegate(
            typeof(Action<ProductFetchFailed>), manager, "OnProductsFetchFailed");
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(owner);

    [Test]
    public void ReopeningShopAfterFetchFailureLoadsPrices()
    {
        manager.Prepare();
        products.Fail();
        Assert.IsNull(manager.Price("com.thnha.bettafishaquarium.coins_1000"));
        manager.Prepare();
        Assert.AreEqual(2, products.Requests, "Reopening must request products again after failure.");
        products.Succeed();
        Assert.AreEqual("$0.99", manager.Price("com.thnha.bettafishaquarium.coins_1000"));
        manager.Prepare();
        Assert.AreEqual(2, products.Requests, "Loaded prices must be reused.");
    }

    [Test]
    public void ReopeningWhileFetchIsPendingDoesNotDuplicateRequest()
    {
        manager.Prepare();
        manager.Prepare();
        Assert.AreEqual(1, products.Requests);
    }

    [Test]
    public void ReopeningAfterEmptyResponseRetriesProducts()
    {
        manager.Prepare();
        products.CompleteEmpty();
        manager.Prepare();
        Assert.AreEqual(2, products.Requests);
    }

    private sealed class TestProducts : IProductService
    {
        private readonly ObservableCollection<Product> cached = new ObservableCollection<Product>();
        private List<ProductDefinition> requested;
        public int Requests { get; private set; }
        public IAppleStoreExtendedProductService Apple => null;
        public event Action<List<Product>> OnProductsFetched;
        public event Action<ProductFetchFailed> OnProductsFetchFailed;
        public void FetchProducts(List<ProductDefinition> definitions, IRetryPolicy retryPolicy = null)
        {
            Requests++;
            requested = definitions;
        }
        public void FetchProductsWithNoRetries(List<ProductDefinition> definitions) => FetchProducts(definitions);
        public ReadOnlyObservableCollection<Product> GetProducts() => new ReadOnlyObservableCollection<Product>(cached);
        public Product GetProductById(string id) => cached.FirstOrDefault(p => p.definition.id == id);
        public Product GetProductByCatalogListingId(string id) => null;
        public void CompleteEmpty() => OnProductsFetched?.Invoke(new List<Product>());
        public void Fail() => OnProductsFetchFailed?.Invoke((ProductFetchFailed)Activator.CreateInstance(
            typeof(ProductFetchFailed), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { requested, "Test failure" }, null));
        public void Succeed()
        {
            foreach (var definition in requested)
            {
                var product = (Product)Activator.CreateInstance(typeof(Product),
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { definition, new ProductMetadata("$0.99", "Coins", "Coins", "USD", .99m), true }, null);
                cached.Add(product);
            }
            OnProductsFetched?.Invoke(cached.ToList());
        }
    }
}
