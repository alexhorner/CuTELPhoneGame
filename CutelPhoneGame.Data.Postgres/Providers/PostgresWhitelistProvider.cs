﻿using System.Text.RegularExpressions;
using CutelPhoneGame.Core.Enums;
using CutelPhoneGame.Core.Models;
using CutelPhoneGame.Core.Providers;
using CutelPhoneGame.Data.Postgres.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CutelPhoneGame.Data.Postgres.Providers
{
    public class PostgresWhitelistProvider(CutelPhoneGameDbContext db) : IWhitelistProvider
    {
        public Task<bool> ExistsByIdAsync(uint id) => db.NumberWhitelist.AnyAsync(e => e.Id == id);

        public async Task<WhitelistEntryModel?> GetByIdAsync(uint id)
        {
            WhitelistEntry? entry = await db.NumberWhitelist.SingleOrDefaultAsync(e => e.Id == id);

            return entry?.ToModel();
        }

        public Task<int> GetCountAsync() => db.NumberWhitelist.CountAsync();

        public Task<List<WhitelistEntryModel>> GetAllAsync() => db.NumberWhitelist.Select(e => e.ToModel()).ToListAsync();

        public async Task<(List<WhitelistEntryModel> WhitelistEntries, PaginationModel Pagination)> GetAllPaginatedAsync(int page, int limit = 10)
        {
            int offset = page * limit;

            int totalNumberListEntries = await db.NumberWhitelist.CountAsync();
            
            IQueryable<WhitelistEntry> numberListEntriesQuery = db.NumberWhitelist.Skip(offset).Take(limit);
            
            int maxPage = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(totalNumberListEntries) / Convert.ToDouble(limit)) - 1);
            
            if (maxPage < 0) maxPage = 0;
            
            return (await numberListEntriesQuery.Select(e => e.ToModel()).ToListAsync(), new PaginationModel
            {
                CurrentPage = page,
                MaxPage = maxPage,
                TotalItems = totalNumberListEntries
            });
        }

        public bool MatchesNumberAsync(string number)
        {
            bool matches = false;
            
            foreach (WhitelistEntry entry in db.NumberWhitelist)
            {
                switch (entry.Interpretation)
                {
                    case ValueInterpretation.Exact:
                        if (entry.Value == number) matches = true;
                        break;
                    
                    case ValueInterpretation.Prefix:
                        if (number.StartsWith(entry.Value)) matches = true;
                        break;
                    
                    case ValueInterpretation.Suffix:
                        if (number.EndsWith(entry.Value)) matches = true;
                        break;
                    
                    case ValueInterpretation.Regex:
                        if (Regex.IsMatch(number, entry.Value)) matches = true;
                        break;
                }

                if (matches) break;
            }

            return matches;
        }

        public async Task<WhitelistEntryModel> CreateAsync(WhitelistEntryModel model)
        {
            WhitelistEntry newWhitelistEntry = WhitelistEntry.FromModel(model);

            db.NumberWhitelist.Add(newWhitelistEntry);
            
            await db.SaveChangesAsync();

            return newWhitelistEntry.ToModel();
        }

        public async Task DeleteAsync(uint id)
        {
            await using IDbContextTransaction tran = await db.Database.BeginTransactionAsync();

            try
            {
                WhitelistEntry? numberListEntry = await db.NumberWhitelist.SingleOrDefaultAsync(e => e.Id == id);

                if (numberListEntry is null) throw new KeyNotFoundException($"Unable to find entry with id '{id}'");
            
                db.Remove(numberListEntry);
            
                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                await tran.RollbackAsync();
                throw;
            }

            await tran.CommitAsync();
        }
    }
}