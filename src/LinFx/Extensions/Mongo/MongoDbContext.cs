﻿using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace LinFx.Extensions.Mongo
{
    public class MongoDbContext : IDisposable
    {
        private IMongoDatabase _db;
        private volatile IMongoClient _connection;

        readonly MongoDbOptions _options;
        readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        public MongoDbContext(IOptions<MongoDbOptions> options)
        {
            _options = options.Value;
        }

        private void Connect()
        {
            if (_db != null)
                return;

            _connectionLock.Wait();

            try
            {
                if(_db == null)
                {
                    _connection = new MongoClient(_options.Configuration);
                    _db = _connection.GetDatabase(_options.Name);
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public IMongoCollection<T> GetCollection<T>()
        {
            string name;
            var type = typeof(T);
            var tableAttrs = type.GetCustomAttributes(typeof(TableAttribute), false) as TableAttribute[];
            if (tableAttrs.Length > 0)
                name = tableAttrs[0].Name;
            else
                name = type.Name;

            Connect();

            return _db.GetCollection<T>(name);
        }

        public Task InsertAsync<T>(T item)
        {
            return GetCollection<T>().InsertOneAsync(item);
        }

        public async Task<long> DeleteAsync<T>(Expression<Func<T, bool>> filter)
        {
            try
            {
                var result = await GetCollection<T>().DeleteOneAsync(filter);
                return result.DeletedCount;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection = null;
            }
        }
    }
}