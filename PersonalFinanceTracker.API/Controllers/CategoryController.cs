using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceTrackerAPI.Interfaces;
using PersonalFinanceTrackerAPI.Models;

namespace PersonalFinanceTrackerAPI.Controllers
{
  [Route("api/v{version:apiVersion}/[controller]")]
  [ApiVersion("1.0")]
  [EnableRateLimiting("SlidingWindowPolicy")]
  [ApiController]
  [Authorize]
  public class CategoryController : ControllerBase
  {
    private readonly ICategoryService _categoryService;
    public CategoryController(ICategoryService categoryService)
    {
      _categoryService = categoryService;
     
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryResponseDTO>>> GetAllCategoriesAsync()
    {
      try
      {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
          return Unauthorized(new ErrorResponse
          {
            ErrorCode = "401",
            ErrorMessage = "No se ha proporcionado un token de autenticación válido o ha expirado. Por favor, inicie sesión nuevamente."
          });
        }
        var categories = await _categoryService.GetAllCategoriesAsync(userId);
        return Ok(categories);
      }
      catch (Exception ex)
      {
        return BadRequest(new ErrorResponse
        {
          ErrorCode = "400",
          ErrorMessage = ex.Message
        });
      }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDTO>> GetCategoryById(string id)
    {
      try
      {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
          return Unauthorized();
        }
        var category = await _categoryService.GetCategoryByIdAsync(id, userId);

        return Ok(category);
      }
      catch (Exception ex)
      {
        return BadRequest(new ErrorResponse
        {
          ErrorCode = "400",
          ErrorMessage = ex.Message
        });
      }
    }

    [HttpPost]
    public async Task<ActionResult<CategoryResponseDTO>> CreateCategory([FromBody] CategoryDTO category)
    {
      try
      {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
          return Unauthorized();
        }
   
        var createdCategory = await _categoryService.CreateCategoryAsync(category, userId);

        var categoriResponse = new CategoryResponseDTO
        (
          createdCategory.CategoryId,
          createdCategory.Name
        );

        return Ok(categoriResponse);
      }
      catch (Exception ex)
      {
        return BadRequest(new ErrorResponse
        {
          ErrorCode = "400",
          ErrorMessage = ex.Message
        });
      }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAsync(string id)
    {
      try
      {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
          return Unauthorized();
        }
        var category = await _categoryService.RemoveCategoryAsync(id, userId);
        return NoContent();
      }
      catch (KeyNotFoundException ex)
      {
        return NotFound(new ErrorResponse
        {
          ErrorCode = "404",
          ErrorMessage = ex.Message
        });
      }
      catch (Exception ex)
      {
        return BadRequest(new ErrorResponse
        {
          ErrorCode = "400",
          ErrorMessage = ex.Message
        });
      }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CategoryResponseDTO>> PutAsync(string id, [FromBody] CategoryDTO category)
    {
      try
      {
        if (!ModelState.IsValid)
        {
          return BadRequest(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
          return Unauthorized();
        }

        var updatedCategory = await _categoryService.UpdateTransactionAsync(id, category, userId);
        return Ok(updatedCategory);
      }
      catch (KeyNotFoundException ex)
      {
        return NotFound(new ErrorResponse
        {
          ErrorCode = "404",
          ErrorMessage = ex.Message
        });
      }
      catch (Exception ex)
      {
        return BadRequest(new ErrorResponse
        {
          ErrorCode = "400",
          ErrorMessage = ex.Message
        });
      }
    }
  }
}
