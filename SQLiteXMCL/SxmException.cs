using System;
using Microsoft.Data.Sqlite;

namespace SQLiteXM
{
    public class SxmException : Exception
    {
        public SxmException()
        {
        }

        public SxmException(ErrorMessage ErrorMessage)
            : base(ErrorMessage.ErrorText)
        {
            this.Data.Add("sxmErrorCode", ErrorMessage.ErrorID);
        }

        public SxmException(Exception inner)
            : base(inner.Message, inner)
        {
            this.Data.Add("sxmErrorCode", ErrorMessages.error["innerException"].ErrorID);
        }

        public SxmException(Microsoft.Data.Sqlite.SqliteException sqliteException)
            : base(sqliteException.Message)
        {
            this.Data.Add("sxmErrorCode", ErrorMessages.error["SqliteException"].ErrorID);
            this.Data.Add("sqliteErrorCode", sqliteException.ErrorCode);
        }

        public static Exception getInnermostException(Exception ex)
        {
            Exception iEX = ex;

            while (ex.InnerException != null)
                iEX = ex.InnerException;

            return iEX;
        }
    }
}


