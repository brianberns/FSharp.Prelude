namespace Prelude.Operators.Option

[<AutoOpen>]
module OptionOperators =

    /// Infix map operator.
    let inline (<!>) (f: 'a -> 'b) (option: 'a option) : 'b option = Option.map f option

    /// Infix apply operator.
    let inline (<*>) (f: ('a -> 'b) option) (option: 'a option) : 'b option =
        match f, option with
        | Some f', Some something -> Some(f' something)
        | None, _ -> None
        | _, None -> None

    /// Infix bind operator.
    let inline (>>=) (option: 'a option) (f: 'a -> 'b option) : 'b option = Option.bind f option


namespace Prelude

open Prelude.Operators.Option

[<RequireQualifiedAccess>]
module Option =

    let singleton (value: 'a) : 'a option = Some value

    let apply (f: ('a -> 'b) option) (option: 'a option) : 'b option = f <*> option

    let andMap (option: 'a option) (f: ('a -> 'b) option) : 'b option = Option.map2 (|>) option f

    let rec private traverser (f: 'a -> Option<'b>) folder state xs =
        match xs with
        | [] -> List.rev <!> state
        | head :: tail ->
            folder head state
            |> function
                | Some _ as this -> traverser f folder this tail
                | None as this -> this

    let traverse (f: 'a -> Option<'b>) (options: 'a list) : Option<'b list> =
        let folder head tail =
            f head
            >>= fun head' -> tail >>= fun tail' -> singleton (head' :: tail')

        traverser f folder (singleton []) options

    let sequence (options: Option<'a> list) : Option<'a list> = traverse id options

    let zip (option1: 'a option) (option2: 'b option) : ('a * 'b) option =
        (fun a b -> a, b) <!> option1 <*> option2

    let zip3 option1 option2 option3 =
        (fun a b c -> a, b, c) <!> option1 <*> option2 <*> option3

    /// Splits an optioned tuple into two options.
    let unzip = function
        | Some (a, b) -> Some a, Some b
        | None -> None, None

    /// Splits an optioned tuple into three options.
    let unzip3 = function
        | Some (a, b, c) -> Some a, Some b, Some c
        | None -> None, None, None

    let ofResult (result: Result<'a, 'b>) : 'a option =
        match result with
        | Ok ok -> Some ok
        | Error _ -> None

    let ofChoice (choice: Choice<'a, 'b>) : 'a option =
        match choice with
        | Choice1Of2 left -> Some left
        | Choice2Of2 _ -> None

    /// Creates a safe version of the supplied function, returning None instead of throwing an exception.
    let ofThrowable (f: 'a -> 'b) (a: 'a) : 'b option =
        try
            Some(f a)
        with
        | _ -> None

[<AutoOpen>]
module OptionCE =

    type OptionBuilder() =

        member _.Return(value) : 'a option = Option.singleton value

        member _.ReturnFrom(option: 'a option) : 'a option = option

        member _.Zero() : unit option = Option.singleton ()

        member _.Bind(option: 'a option, f: 'a -> 'b option) : 'b option = Option.bind f option

        member _.Delay(f: unit -> 'a option) : unit -> 'a option = f

        member _.Run(f: unit -> 'a option) : 'a option = f ()

        member _.Combine(option: 'a option, f: 'a -> 'b option) : 'b Option = Option.bind f option

        member this.TryWith(f: unit -> 'a option, g: exn -> 'a option) : 'a option =
            try
                this.Run f
            with
            | exn -> g exn

        member this.TryFinally(f: unit -> 'a option, g: unit -> unit) : 'a option =
            try
                this.Run f
            finally
                g ()

        member this.Using(disposable: 'a :> System.IDisposable, f: 'a -> 'a option) : 'a option =
            this.TryFinally(
                (fun () -> f disposable),
                fun () ->
                    if not (obj.ReferenceEquals(disposable, null)) then
                        disposable.Dispose()
            )

        member this.While(f: unit -> bool, g: unit -> Option<unit>) : Option<unit> =
            if not (f ()) then
                this.Zero()
            else
                this.Run g
                |> Option.bind (fun () -> this.While(f, g))

        member _.BindReturn(option: 'a option, f: 'a -> 'b) : 'b option = Option.map f option

        member _.MergeSources(option1: 'a option, option2: 'b option) = Option.zip option1 option2

    let option = OptionBuilder()
