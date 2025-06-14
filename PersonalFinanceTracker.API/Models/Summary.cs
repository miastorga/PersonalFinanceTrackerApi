using System;

namespace PersonalFinanceTrackerAPI.Models;

public class Summary
{
  // Básico
  public int TotalIncome { get; set; }
  public int TotalExpenses { get; set; }
  public int Balance { get; set; }
    
  // Período
  public DateTime StartDate { get; set; }
  public DateTime EndDate { get; set; }
  // Los periodos serian daily, weekly, monthly y yearly
  public string Period { get; set; }
    
  // Desglose
  public Dictionary<string, int> ExpensesByCategory { get; set; }
  public Dictionary<string, int> IncomeByCategory { get; set; }
  public Dictionary<string, int> BalanceByAccount { get; set; }
    
  // Métricas adicionales
  public int TransactionCount { get; set; }
  public int AverageTransactionAmount { get; set; }
  public string TopExpenseCategory { get; set; }
  public string TopIncomeCategory { get; set; }
    
  // Comparación
  public int PreviousPeriodBalance { get; set; }
  public int BalanceChange { get; set; }
  public decimal BalanceChangePercentage { get; set; }
}
