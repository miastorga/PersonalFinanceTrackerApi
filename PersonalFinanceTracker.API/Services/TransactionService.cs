using System;
using System.Transactions;
using PersonalFinanceTrackerAPI.Data;
using PersonalFinanceTrackerAPI.Interfaces;
using PersonalFinanceTrackerAPI.Models;

namespace PersonalFinanceTrackerAPI.Services;

public class TransactionService : ITransactionService
{
  private readonly ITransactionRepository _transactionRepository;
  private readonly ICategoryService _categoryService;
  private readonly IAccountService _accountService;
  private readonly AppDbContext _context;
  public TransactionService(ITransactionRepository transaction, ICategoryService categoryService, IAccountService accountService, AppDbContext context)
  {
    _transactionRepository = transaction;
    _categoryService = categoryService;
    _accountService = accountService;
    _context = context;
  }
  public async Task<TransactionResponseDTO> CreateTransactionAsync(TransactionDTO transactionDto, string userId)
  {
    var transaction = new Transactions
    {
      TransactionId = Guid.NewGuid().ToString(),
      UserId = userId,
      Amount = transactionDto.Amount,
      TransactionType = transactionDto.TransactionType,
      CategoryId = transactionDto.CategoryId,
      Date = transactionDto.Date,
      Description = transactionDto.Description,
      AccountId = string.IsNullOrEmpty(transactionDto.AccountId) ? null : transactionDto.AccountId
    };

    if (!string.IsNullOrEmpty(transactionDto.AccountId))
    {
      var account = await _accountService.GetAccountByIdAsync(transactionDto.AccountId, userId);
      account.CurrentBalance += transactionDto.Amount;
      var updateAccount = await _accountService.UpdateAccountAsync(transactionDto.AccountId, account,userId);
      
      await _transactionRepository.AddAsync(transaction);
      var categoryById = await _categoryService.GetCategoryByIdAsync(transaction.CategoryId, userId);
      var responseDTO = new TransactionResponseDTO(
        transaction.TransactionId,
        transaction.Amount,
        transaction.TransactionType,
        transaction.CategoryId,
        categoryById.Name,
        transaction.Date,
        transaction.Description,
        transaction.AccountId,
        account.AccountName
      );

      return responseDTO;
    }
    
    await _transactionRepository.AddAsync(transaction);
    var category = await _categoryService.GetCategoryByIdAsync(transaction.CategoryId, userId);
    // account = await _accountService.GetAccountByIdAsync(transaction.AccountId, userId);

    var response = new TransactionResponseDTO(
      transaction.TransactionId,
      transaction.Amount,
      transaction.TransactionType,
      transaction.CategoryId,
      category.Name,
      transaction.Date,
      transaction.Description,
      null,null
    );

    return response;
  }

  public async Task<PaginatedList<TransactionResponseDTO>> GetAllTransactions(string userId, int page, int results, DateTime? startDate, DateTime? endDate, string? categoryName, string? transactionType)
  {
    var transactions = await _transactionRepository.GetAllTransactionsAsync(userId, page, results, startDate, endDate, categoryName, transactionType);
    return transactions;
  }

  public async Task<TransactionResponseDTO> GetTransactionByIdAsync(string id, string userId)
  {
    var transaction = await _transactionRepository.GetByIdAsync(id, userId);
    var category = await _categoryService.GetCategoryByIdAsync(transaction.CategoryId, userId);
    var account = await _accountService.GetAccountByIdAsync(transaction.AccountId, userId);
    var response = new TransactionResponseDTO(
           transaction.TransactionId,
           transaction.Amount,
           transaction.TransactionType,
           transaction.CategoryId,
           category.Name,
           transaction.Date,
           transaction.Description,
           transaction.AccountId,
           account.AccountName
       );

    return response;
  }

public async Task<Summary> GetTransactionSummaryAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, string period = null)
{
    // Si no se proporcionan fechas, usar el mes actual por defecto
    var summaryStartDate = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    var summaryEndDate = endDate ?? summaryStartDate.AddMonths(1).AddDays(-1);
    
    summaryStartDate = DateTime.SpecifyKind(summaryStartDate, DateTimeKind.Utc);
    summaryEndDate = DateTime.SpecifyKind(summaryEndDate, DateTimeKind.Utc);

    // Detectar período automáticamente si no se especifica
    var detectedPeriod = period ?? DetectPeriodFromDateRange(summaryStartDate, summaryEndDate);
    
    // Obtener transacciones del período actual
    var transactions = await _transactionRepository.GetTransactionsByDateRangeAsync(userId, summaryStartDate, summaryEndDate);
    var categories = await _categoryService.GetAllCategoriesAsync(userId);
    var accounts = await _accountService.GetAllAccountsAsync(userId);
    
    if (transactions == null) transactions = new List<Transactions>();
    if (categories == null) categories = new List<CategoryResponseDTO>();
    if (accounts == null) accounts = new List<AccountResponseDTO>();
    
    var categoryNames = categories.ToDictionary(c => c.CategoryId, c => c.CategoryName);
    // var accountNames = accounts.ToDictionary(a => a.AccountId, a => a.AccountName);
    
    // Cálculos básicos
    var totalIncome = transactions
        .Where(t => t.TransactionType == "ingreso")
        .Sum(t => t.Amount);
        
    var totalExpenses = transactions
        .Where(t => t.TransactionType == "gasto")
        .Sum(t => t.Amount);
        
    // TODO revisar balance, balanceChange y balanceChangePercentage
    var balance = totalIncome - totalExpenses;
    
    // Agrupar por categorías
    var expensesByCategory = transactions
        .Where(t => t.TransactionType == "gasto" && !string.IsNullOrEmpty(t.CategoryId))
        .GroupBy(t => t.CategoryId)
        .ToDictionary(
            g => categoryNames.GetValueOrDefault(g.Key, "Sin Categoría"),
            g => g.Sum(t => t.Amount)
        );
        
    var incomeByCategory = transactions
        .Where(t => t.TransactionType == "ingreso" && !string.IsNullOrEmpty(t.CategoryId))
        .GroupBy(t => t.CategoryId)
        .ToDictionary(
            g => categoryNames.GetValueOrDefault(g.Key, "Sin Categoría"),
            g => g.Sum(t => t.Amount)
        );
    
    // Balance por cuentavar balanceByAccount = accounts
    var balanceByAccount = accounts
        .GroupBy(a => a.AccountName)
        .ToDictionary(
            g => g.Key,
            g => g.Sum(a => a.CurrentBalance)
        );
    
    // Métricas adicionales
    var transactionCount = transactions.Count();
    var averageTransactionAmount = transactionCount > 0 ? (int)(transactions.Sum(t => t.Amount) / transactionCount) : 0;
    
    var topExpenseCategory = expensesByCategory.Any() 
        ? expensesByCategory.OrderByDescending(x => x.Value).First().Key 
        : "N/A";
        
    var topIncomeCategory = incomeByCategory.Any() 
        ? incomeByCategory.OrderByDescending(x => x.Value).First().Key 
        : "N/A";
    
    // Calcular período anterior para comparación usando el período detectado
    var (previousStartDate, previousEndDate) = GetPreviousPeriodDates(summaryStartDate, summaryEndDate, detectedPeriod);
    
    var previousTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(userId, previousStartDate, previousEndDate);
    var previousBalance = previousTransactions.Where(t => t.TransactionType == "ingreso").Sum(t => t.Amount) 
                         - previousTransactions.Where(t => t.TransactionType == "gasto").Sum(t => t.Amount);
    
    var balanceChange = balance - previousBalance;
    var balanceChangePercentage = previousBalance != 0 ? (int)((decimal)balanceChange / Math.Abs(previousBalance) * 100) : 0;
    
    return new Summary
    {
        // Básico
        TotalIncome = totalIncome,
        TotalExpenses = totalExpenses,
        Balance = balance,
        
        // Período
        StartDate = summaryStartDate,
        EndDate = summaryEndDate,
        Period = detectedPeriod,
        
        // Desglose
        ExpensesByCategory = expensesByCategory,
        IncomeByCategory = incomeByCategory,
        BalanceByAccount = balanceByAccount,
        
        // Métricas adicionales
        TransactionCount = transactionCount,
        AverageTransactionAmount = averageTransactionAmount,
        TopExpenseCategory = topExpenseCategory,
        TopIncomeCategory = topIncomeCategory,
        
        // Comparación
        PreviousPeriodBalance = previousBalance,
        BalanceChange = balanceChange,
        BalanceChangePercentage = balanceChangePercentage
    };
}

private string DetectPeriodFromDateRange(DateTime startDate, DateTime endDate)
{
    var daysDifference = (endDate - startDate).Days + 1; // +1 para incluir ambos días
    
    return daysDifference switch
    {
        1 => "daily",
        >= 2 and <= 7 => "weekly",
        >= 8 and <= 31 => "monthly",
        >= 32 and <= 92 => "quarterly", // ~3 meses
        >= 93 and <= 186 => "half-year", // ~6 meses
        >= 187 and <= 366 => "yearly",
        _ => "custom" // Para períodos muy largos o atípicos
    };
}

// Método mejorado para calcular períodos anteriores
private (DateTime startDate, DateTime endDate) GetPreviousPeriodDates(DateTime currentStart, DateTime currentEnd, string period)
{
    var daysDifference = (currentEnd - currentStart).Days;
    
    return period.ToLower() switch
    {
        "daily" => (currentStart.AddDays(-1), currentEnd.AddDays(-1)),
        "weekly" => (currentStart.AddDays(-7), currentEnd.AddDays(-7)),
        "monthly" => (currentStart.AddMonths(-1), currentEnd.AddMonths(-1)),
        "quarterly" => (currentStart.AddMonths(-3), currentEnd.AddMonths(-3)),
        "half-year" => (currentStart.AddMonths(-6), currentEnd.AddMonths(-6)),
        "yearly" => (currentStart.AddYears(-1), currentEnd.AddYears(-1)),
        "custom" => (
            currentStart.AddDays(-(daysDifference + 1)), 
            currentStart.AddDays(-1)
        ), // Para períodos custom, toma el mismo rango de días justo antes
        _ => (currentStart.AddDays(-(daysDifference + 1)), currentStart.AddDays(-1))
    };
}

// Métodos auxiliares originales (mantenidos para compatibilidad)
private DateTime GetPreviousPeriodStart(DateTime currentStart, string period)
{
    return period.ToLower() switch
    {
        "daily" => currentStart.AddDays(-1),
        "weekly" => currentStart.AddDays(-7),
        "monthly" => currentStart.AddMonths(-1),
        "quarterly" => currentStart.AddMonths(-3),
        "yearly" => currentStart.AddYears(-1),
        _ => currentStart.AddMonths(-1)
    };
}

private DateTime GetPreviousPeriodEnd(DateTime currentEnd, string period)
{
    return period.ToLower() switch
    {
        "daily" => currentEnd.AddDays(-1),
        "weekly" => currentEnd.AddDays(-7),
        "monthly" => currentEnd.AddMonths(-1),
        "quarterly" => currentEnd.AddMonths(-3),
        "yearly" => currentEnd.AddYears(-1),
        _ => currentEnd.AddMonths(-1)
    };
}

// Sobrecarga para mantener compatibilidad
public async Task<Summary> GetTransactionSummaryAsync(string userId)
{
    return await GetTransactionSummaryAsync(userId, null, null, "monthly");
}

public async Task<bool> RemoveTransactionAsync(string id, string userId)
  {
    var transactionToDelete = await _transactionRepository.GetByIdAsync(id, userId);

    var amount = transactionToDelete.Amount; 
    var type = transactionToDelete.TransactionType; 
    var accountId = transactionToDelete.AccountId; 

    var account = await _context.Accounts.FindAsync(accountId);
    if (account == null)
    {
      _context.Transactions.Remove(transactionToDelete);
      await _context.SaveChangesAsync();
      return true;
    }

    var impact = (type.ToLower() == "ingreso") ? amount : -amount;
    account.CurrentBalance -= impact;

    _context.Accounts.Update(account); 

    _context.Transactions.Remove(transactionToDelete); 

    await _context.SaveChangesAsync();

    return true;
  }

  public async Task<TransactionDTO> UpdateTransactionAsync(string id, TransactionDTO transactionDto, string userId)
  {
    var existingTransaction = await _transactionRepository.GetByIdAsync(id, userId);
    if (existingTransaction == null)
    {
        throw new InvalidOperationException($"Transaction with ID {id} not found for user {userId}.");
    }

    await using var dbTransaction = await _transactionRepository.BeginTransactionAsync();
    try
    {
      var oldAmount = existingTransaction.Amount;
      var oldType = existingTransaction.TransactionType;
      var oldAccountId = existingTransaction.AccountId;
    
      var newAmount = transactionDto.Amount;
      var newType = transactionDto.TransactionType;
      var newAccountId = transactionDto.AccountId;
    
      if (!string.IsNullOrEmpty(oldAccountId))
      {
        var oldAccount = await _accountService.GetAccountByIdAsync(oldAccountId, userId);
        if (oldAccount == null)
        {
          throw new InvalidOperationException($"Account with ID {oldAccountId} not found for user {userId}.");
        }
        
        var oldImpact = (oldType.ToLower() == "ingreso") ? oldAmount : -oldAmount;
        oldAccount.CurrentBalance -= oldImpact;
        await _accountService.UpdateAccountAsync(oldAccountId, oldAccount, userId);
      }
    
      _transactionRepository.UpdateAync(existingTransaction, transactionDto);
    
      if (!string.IsNullOrEmpty(newAccountId))
      {
        var newAccount = await _accountService.GetAccountByIdAsync(newAccountId, userId);
        if (newAccount == null)
        {
          throw new InvalidOperationException($"New account with ID {newAccountId} not found for user {userId}.");
        }
        
        var newImpact = (newType.ToLower() == "ingreso") ? newAmount : -newAmount;
        newAccount.CurrentBalance += newImpact;
        await _accountService.UpdateAccountAsync(newAccountId, newAccount, userId);
      }
    
      await _transactionRepository.SaveChangesAsync();

      await _transactionRepository.CommitTransactionAsync(dbTransaction);
    
      var updatedTransactionDTO = new TransactionDTO
      (
        existingTransaction.Amount,
        existingTransaction.TransactionType,
        existingTransaction.CategoryId,
        existingTransaction.Date,
        existingTransaction.Description,
        existingTransaction.AccountId
      );
    
      return updatedTransactionDTO;
    }
    catch
    {
      await _transactionRepository.RollbackTransactionAsync(dbTransaction);
      throw;
    }
  }
}
