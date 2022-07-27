﻿using EShopOnAbp.CatalogService.Products;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace EShopOnAbp.PublicWeb.Pages
{
    public class ProductDetailModel : AbpPageModel
    {
        [BindProperty(SupportsGet = true)]
        public int OrderNo { get; set; }

        public ProductDto Product { get; private set; }
        public bool HasRemoteServiceError { get; set; } = false;
        private readonly IPublicProductAppService _productAppService;


        public ProductDetailModel(IPublicProductAppService productAppService)
        {
            _productAppService = productAppService;
        }

        public async Task OnGet(Guid id)
        {
            try
            {
                Product = await _productAppService.GetAsync(id);
            }
            catch (Exception e)
            {
                Product = new ProductDto();
                HasRemoteServiceError = true;
                Console.WriteLine(e);
            }
        }
    }
}