namespace SubTools;
public static class Common
{
    public static int GetMilliseconds( bool Sign, int Hour, int Minute, int Second, int Millisecond ){
        int result = Hour * 3600000 + Minute *60000 + Second * 1000 + Millisecond;
        if( Sign ){ result = - result;  }
        return result;
    }
    public static int GetMillisecondsFromCentiseconds( bool Sign, int Hour, int Minute, int Second, int Centisecond ){
        int result = Hour * 3600000 + Minute *60000 + Second * 1000 + Centisecond * 10;
        if( Sign ){ result = - result;  }
        return result;
    }

    public static (bool NegativeSign, int Hour, int Minute, int Second, int Millisecond) GetTime( int Millisecond ) {
        (int _MS, bool NegativeSign) = Millisecond < 0 ?(- Millisecond, true) : ( Millisecond, false);
        (int _H, _MS) = Math.DivRem( _MS, 3600000 );
        (int _M, _MS) = Math.DivRem( _MS, 60000 );
        (int _S, _MS) = Math.DivRem( _MS, 1000 );
        return (NegativeSign, _H, _M, _S, _MS );
    }
    public static (bool NegativeSign, int Hour, int Minute, int Second, int Centisecond) GetTimeCentiseconds( int Millisecond ) {
        (int _MS, bool NegativeSign) = Millisecond < 0 ?(- Millisecond, true) : ( Millisecond, false);
        (int _H, _MS) = Math.DivRem( _MS, 3600000 );
        (int _M, _MS) = Math.DivRem( _MS, 60000 );
        (int _S, _MS) = Math.DivRem( _MS, 1000 );
        int _CS = _MS / 10;
        return (NegativeSign, _H, _M, _S, _CS );
    }

    public static (bool NegativeSign, int Hour, int Minute, int Second, int Millisecond) GetTime0( int Millisecond ) {
        int _MS = Millisecond; bool NegativeSign = false;
        if( Millisecond < 0) { _MS = - _MS; NegativeSign = true; }
        int _H = _MS / 3600000;
        _MS ^= _H * 3600000;//need to test correctness, ^ is bitwise XOR, not subtraction.
        int _M = _MS / 60000;
        _MS ^= _M * 60000;
        int _S = _MS / 1000;
        _MS ^= _S * 1000;
        return (NegativeSign, _H, _M, _S, _MS );
    }

    public static bool[] GetEmptyToRemoveList( int Length )
    {
        return new bool[Length];
    }
    //generic method, no type conversion needed (AKA no boxing)
    public static List<T> RemoveFromList<T>( List<T> InputList, bool[] ToRemove )
    {
        if( InputList.Count != ToRemove.Length )
            throw new InvalidDataException("Input list and check list, size mismatch!");
        //set the capacity to the maximum number of items possible ( no greater than InputList.Count )
        var newList = new List<T>( InputList.Count );
        for ( int i = 0; i < InputList.Count; i++ )
        if( ! ToRemove[i] )//don't copy
        {
            newList.Add( InputList[i] );
        }
        //replace our list with the new
        newList.TrimExcess();
        return newList;
        /*alternatively
        return = List.Where((_, index) => ! ToRemove[index] ).ToList();
        */
    }
    public static bool ResolveOutDir( ref string? outputDir )
    {
        if ( ! string.IsNullOrWhiteSpace( outputDir  ) ) {
            //ensure OutputDir exists
            try {
                var directoryInfo = Directory.CreateDirectory(outputDir);
                outputDir = directoryInfo.FullName;
                return true;
            }
            catch (Exception ex) {
                throw new InvalidOperationException($"Failed to create or resolve OutputDir: {ex.Message}", ex);
            }
        }
        //use Directory of the first file in SubFiles
        if( FileLoader.SubFiles.Count > 0 )
        {
            //outputDir = new FileInfo(FileLoader.SubFiles[0].FilePath).DirectoryName;
            outputDir = Path.GetDirectoryName(FileLoader.SubFiles[0].FilePath);
        }
        else
            //use CurrentDirectory
            outputDir = Directory.GetCurrentDirectory();
        
        //only true if original outputDir is used
        return false;
    }
}