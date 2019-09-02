using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Supermarket.API.Domain.Models;
using Supermarket.API.Domain.Services;

namespace Supermarket.API.Controllers
{
    [Route("/api/[controller]")] //In this case we have Categories
    public class CategoriesController : Controller
    {
        //activte DI
         private readonly ICategoryService _categoryService;
        
        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;   
        }
        //activte DI

        [HttpGet]
        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            var categories = await _categoryService.ListAsync();
            return categories;
        }
    }
}