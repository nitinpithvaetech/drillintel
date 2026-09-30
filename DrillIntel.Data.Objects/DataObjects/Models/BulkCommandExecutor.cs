using DrillIntel.Data;
using System;
using System.Collections.Generic;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    /// <summary>
    /// Batches SQLite execution inside transactions.
    /// Provides ~1,000x faster execution than autocommit by reducing disk fsync operations.
    /// </summary>
    public class BulkCommandExecutor
    {
        private readonly IDataServiceDIntel? _dataService;
        private readonly int _batchLimit;
        private readonly List<string> _buffer = new();

        public string LastError { get; private set; } = string.Empty;

        public BulkCommandExecutor()
        {
            _batchLimit = 100;
        }

        public BulkCommandExecutor(IDataServiceDIntel dataService, int batchLimit = 100)
        {
            _dataService = dataService;
            _batchLimit = batchLimit > 0 ? batchLimit : 100;
        }

        public bool ExecuteCommand(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return true;

            _buffer.Add(sql);
            if (_buffer.Count >= _batchLimit)
            {
                return FlushBuffer();
            }
            return true;
        }

        public bool ExecuteCommandWithPause(string sql)
        {
            return ExecuteCommand(sql);
        }

        public bool FlushBuffer()
        {
            if (_dataService == null || _buffer.Count == 0) return true;

            try
            {
                _dataService.BeginTransaction();
                foreach (string sql in _buffer)
                {
                    _dataService.ExecuteNonQuery(sql);
                }
                _dataService.Commit();
                _buffer.Clear();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                try
                {
                    _dataService.RollBack();
                }
                catch { }
                _buffer.Clear();
                return false;
            }
        }

        public BulkCommandExecutor GetCopy() => new BulkCommandExecutor(_dataService!, _batchLimit);
    }
}

