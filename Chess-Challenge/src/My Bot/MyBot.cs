using System;
using System.Linq;
using ChessChallenge.API;



public sealed class MyBot : IChessBot
{
    // Represent +INF and -INF
    private const int MIN_VAL = -999999;
    private const int MAX_VAL = 999999;

    // 2^20 transposition table entries in total
    private const int TT_ENTRIES = (1 << 20);

    enum TTFlag
    {   
        Invalid,// Represents uninitialized TTEntry
        Upper,  // Fail low (the entry score is an upper bound). This means no value better than alpha was found
        Lower,  // Fail high (the entry score is a lower bound). This means a value better than beta was found
        Exact   // The entry score is an exact value, so the value was between alpha and beta
    }

    // Transposition table entries
    private readonly struct TTEntry
    {
        public readonly ulong key;
        public readonly int depth, score;
        public readonly TTFlag flag;
        public TTEntry(ulong _key, int _depth, int _score, TTFlag _flag)
        {
            key = _key; depth = _depth; score = _score; flag = _flag;
        }
    }
    
    // Stores transposition table entries
    private readonly TTEntry[] transpositionTable = new TTEntry[TT_ENTRIES];

    // Precomputed compresed PST values
    private readonly byte[] PSTs = new ulong[]{
        2531906049332683555, 1748981496244382085, 1097852895337720349, 879379754340921365,
        733287618436800776, 1676506906360749833, 957361353080644096, 2531906049332683555,
        1400370699429487872, 7891921272903718197, 12306085787436563023, 10705271422119415669,
        8544333011004326513, 7968995920879187303, 7741846628066281825, 7452158230270339349,
        5357357457767159349, 2550318802336244280, 5798248685363885890, 5789790151167530830,
        6222952639246589772, 6657566409878495570, 6013263560801673558, 4407693923506736945,
        8243364706457710951, 8314078770487191394, 6306293301333023298, 3692787177354050607,
        3480508800547106083, 2756844305966902810, 18386335130924827, 3252248017965169204,
        6871752429727068694, 7516062622759586586, 7737582523311005989, 3688521973121554199,
        3401675877915367465, 3981239439281566756, 3688238338080057871, 5375663681380401,
        5639385282757351424, 2601740525735067742, 3123043126030326072, 2104069582342139184,
        1017836687573008400, 2752300895699678003, 5281087483624900674, 5717642197576017202,
        578721382704613384, 14100080608108000698, 6654698745744944230, 1808489945494790184,
        507499387321389333, 1973657882726156, 74881230395412501, 578721382704613384,
        10212557253393705, 3407899295075687242, 4201957831109070667, 5866904407588300370,
        5865785079031356753, 5570777287267344460, 3984647049929379641, 2535897457754910790,
        219007409309353485, 943238143453304595, 2241421631242834717, 2098155335031661592,
        1303832920857255445, 870353785759930383, 3397624511334669, 726780562173596164,
        1809356472696839713, 1665231324524388639, 1229220018493528859, 1590638277979871000,
        651911504053672215, 291616928119591952, 1227524515678129678, 6763160767239691,
        4554615069702439202, 3119099418927382298, 3764532488529260823, 5720789117110010158,
        4778967136330467097, 3473748882448060443, 794625965904696341, 150601370378243850,
        4129336036406339328, 6152322103641660222, 6302355975661771604, 5576700317533364290,
        4563097935526446648, 4706642459836630839, 4126790774883761967, 2247925333337909269,
        17213489408, 6352120424995714304, 982348882 }
                    .SelectMany(BitConverter.GetBytes).ToArray();


    // MVV-LVA values table
    // Indexed by [VICTIM][ATTACKER]
    private readonly short[,] MVV_LVA = new short[,]
        {
        {0, 0, 0, 0, 0, 0, 0},          // victim None, attacker None, P, N, B, R, Q, K
        {0, 15, 14, 13, 12, 11, 10},    // victim P, attacker None, P, N, B, R, Q, K
        {0, 25, 24, 23, 22, 21, 20},    // victim N, attacker None, P, N, B, R, Q, K
        {0, 35, 34, 33, 32, 31, 30},    // victim B, attacker None, P, N, B, R, Q, K
        {0, 45, 44, 43, 42, 41, 40},    // victim R, attacker None, P, N, B, R, Q, K
        {0, 55, 54, 53, 52, 51, 50},    // victim Q, attacker None, P, N, B, R, Q, K
        {0, 0, 0, 0, 0, 0, 0},          // victim K, attacker None, P, N, B, R, Q, K
        };
       

    // Checks if time for current turn exceeded
    private bool TurnTimeExceeded(Timer timer)
    {
        return timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30;
    }

    // Evaluates a board state by using PeSTO's evaluation function
    private int EvalBoard(Board board)
    {
        int midGame = 0, endGame = 0, phase = 0;

        // Iterates for both blacks and whites
        foreach (bool evalWhites in new[] { true, false })
        {   
            // Iterates over all piece types
            for (var p = PieceType.Pawn; p <= PieceType.King; p++)
            {
                int piece = (int)p - 1, index;
                ulong mask = board.GetPieceBitboard(p, evalWhites);

                // For each piece of PieceType, add their values.
                while (mask != 0)
                {
                    phase += PSTs[768 + piece];
                    // Gets index and also clears LSB of mask
                    index = 64 * piece + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (evalWhites ? 56 : 0);
                    midGame += PSTs[index] + (47 << piece) + PSTs[piece + 776];
                    endGame += PSTs[index + 384] + (47 << piece) + PSTs[piece + 782];
                }

            }

            midGame = -midGame;
            endGame = -endGame;
        }

        return (midGame * phase + endGame * (24 - phase)) / 24 * (board.IsWhiteToMove ? 1 : -1);
    }

    // Negamax + alpha-beta pruning + quiescence search
    private int NegaMax(Board board, int depth, int alpha, int beta, int ply, Timer timer)
    {
        bool isRoot = ply == 0;

        // Check for repetition
        if (!isRoot && board.IsRepeatedPosition() || board.IsDraw())
        {
            return 0;
        }

        if(board.IsInCheckmate())
        {
            return MIN_VAL + ply;
        }

        bool shouldQsearch = depth <= 0;
        ulong zobristKey = board.ZobristKey;
        int bestEval = MIN_VAL;

        // Look for previous transposition table findings.
        // Use modulo operator to access it since key can be bigger than TT_ENTRIES
        TTEntry ttEntry = transpositionTable[zobristKey % TT_ENTRIES];

        // Don't try to use key on ply == 0
        // Only take it into consideration if depth of the entry > current depth
        if (!isRoot && ttEntry.key == zobristKey && ttEntry.depth >= depth && (
           ttEntry.flag == TTFlag.Exact
               || ttEntry.flag == TTFlag.Lower && ttEntry.score >= beta  // Lower bound, fail high
               || ttEntry.flag == TTFlag.Upper && ttEntry.score <= alpha // Upper bound, fail low
       )) return ttEntry.score;
        // If we failed high, we know the score was better than beta when found
        // If we failed low, we know the score was worse tha alpha when found

        // By default, we assume that the transposition table flag is Upper (fail low), since we haven't yet
        // discovered a value better than alpha
        var ttFlag = TTFlag.Upper;

        // If we find a board evaluation >= beta during qsearch, return that value
        if (shouldQsearch)
        {
            bestEval = EvalBoard(board);
            alpha = Math.Max(alpha, bestEval);
            if (alpha >= beta) return bestEval;
        }

        // If shouldQsearch is true, we'll only get capture moves
        var moves = GetOrderedMoves(board, shouldQsearch);

        
        foreach (Move move in moves)
        {
            if (TurnTimeExceeded(timer)) return MAX_VAL;

            board.MakeMove(move);
            var score = -NegaMax(board, depth - 1, -beta, -alpha, ply + 1, timer);
            board.UndoMove(move);


            if (score > bestEval)
            {
                bestEval = score;

                // Improve alpha
                if (score > alpha)
                {   
                    // If there is a score better than alpha, then flag is, at least, Exact, since right now alpha < score < beta
                    ttFlag = TTFlag.Exact;
                    alpha = score;
                }

                // Fail-high
                if (alpha >= beta)
                {   
                    // If we fail-high, then there is a value better than beta, so flag is Lower
                    ttFlag = TTFlag.Lower;
                    break;
                }
            }
        }

        // Push bestEval to TT. Replace by depth (replace only if the depth is better)
        var existingTTEntry = transpositionTable[zobristKey % TT_ENTRIES];
        
        // Check if either depth is enough to replace OR the entry is not initialized (flag = 0 for uninitialized entries in the array)
        if ((existingTTEntry.depth < depth && existingTTEntry.key == zobristKey) || existingTTEntry.flag == TTFlag.Invalid)
        {
            transpositionTable[zobristKey % TT_ENTRIES] = new TTEntry(zobristKey, depth, bestEval, ttFlag);
        }

        return bestEval;


    }

    
    private Move[] GetOrderedMoves(Board board, bool onlyCaptures)
    {   
        var allMoves = board.GetLegalMoves();

        // Order captures by using MVV_LVA
        if (onlyCaptures)
        {
            return allMoves.Where(move => move.IsCapture).OrderByDescending(move => MVV_LVA[(int)move.CapturePieceType, (int)move.MovePieceType]).ToArray();
        }
        else
        {
            var captureMoves = allMoves.Where(move => move.IsCapture).OrderByDescending(move => MVV_LVA[(int)move.CapturePieceType, (int)move.MovePieceType]).ToArray();
            var nonCaptureMoves = allMoves.Except(captureMoves).ToArray();

            return captureMoves.Concat(nonCaptureMoves).ToArray();
        }
    }


    public Move Think(Board board, Timer timer)
    {

        Move[] possibleMoves = GetOrderedMoves(board, false);

        Move bestMove = Move.NullMove;
        int bestEval = MIN_VAL;

        // Iterative deepening, with time limit
        for (int depth = 1; depth <= MAX_VAL; depth++)
        {

            if (TurnTimeExceeded(timer)) break;

            foreach (Move move in possibleMoves)

            {
                if (TurnTimeExceeded(timer)) break;

                board.MakeMove(move);
                int moveEval = -NegaMax(board, depth, MIN_VAL, MAX_VAL, 0, timer);
                board.UndoMove(move);


                if (moveEval > bestEval)
                {
                    bestMove = move;
                    bestEval = moveEval;
                }

            }
        }

        return !bestMove.IsNull ? bestMove : possibleMoves[0];
    }


}
