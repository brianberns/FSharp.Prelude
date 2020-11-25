namespace FSharp.Prelude

open System.Threading.Tasks

module Async =
    let singleton (value: 'a): Async<'a> = async.Return(value)

    let bind (f: 'a -> Async<'b>) (asyncOp: Async<'a>): Async<'b> = async.Bind(asyncOp, f)

    let map (f: 'a -> 'b) (asyncOp: Async<'a>): Async<'b> = bind (f >> singleton) asyncOp

    let apply (f: Async<('a -> 'b)>) (asyncOp: Async<'a>): Async<'b> =
        async {
            let! runF = Async.StartChild f
            let! runAsyncOp = Async.StartChild asyncOp
            let! f' = runF
            let! asyncOpRes = runAsyncOp
            return f' asyncOpRes
        }

    let andMap (asyncOp: Async<'a>) (f: Async<('a -> 'b)>): Async<'b> = apply f asyncOp

    [<AutoOpen>]
    module AsyncOperators =
        let (<!>) (f: 'a -> 'b) (asyncOp: Async<'a>): Async<'b> = map f asyncOp

        let (<*>) (f: Async<('a -> 'b)>) (asyncOp: Async<'a>): Async<'b> = apply f asyncOp

        let (>>=) (f: 'a -> Async<'b>) (asyncOp: Async<'a>): Async<'b> = bind f asyncOp

    let map2 (f: 'a -> 'b -> 'c) (asyncOp1: Async<'a>) (asyncOp2: Async<'b>): Async<'c> = f <!> asyncOp1 <*> asyncOp2

    let sequence (asyncOps: Async<'a> list): Async<'a list> =
        List.foldr (fun asyncOp1 asyncOp2 -> List.cons <!> asyncOp1 <*> asyncOp2) (singleton []) asyncOps

    let zip (asyncOp1: Async<'a>) (asyncOp2: Async<'b>): Async<'a * 'b> =
        (fun a b -> a, b) <!> asyncOp1 <*> asyncOp2

    /// A replacement for Async.AwaitTask that throws inner exceptions if they exist.
    let awaitTaskWithInnerException (task: Task<'T>): Async<'T> =
        Async.FromContinuations(fun (success, exception', _cancellationToken) ->
            task.ContinueWith(fun (t: Task<'T>) ->
                if t.IsFaulted then
                    if t.Exception.InnerExceptions.Count = 1
                    then exception' t.Exception.InnerExceptions.[0]
                    else exception' t.Exception
                elif t.IsCanceled then
                    exception' (TaskCanceledException())
                else
                    success t.Result)
            |> ignore)

    /// A replacement for Async.AwaitTask that throws inner exceptions if they exist.
    let awaitUnitTaskWithInnerException (task: Task): Async<unit> =
        Async.FromContinuations(fun (success, exception', _cancellationToken) ->
            task.ContinueWith(fun (t: Task) ->
                if t.IsFaulted then
                    if t.Exception.InnerExceptions.Count = 1
                    then exception' t.Exception.InnerExceptions.[0]
                    else exception' t.Exception
                elif t.IsCanceled then
                    exception' (TaskCanceledException())
                else
                    success ())
            |> ignore)