open System.IO
let toValidPath (s: string) =
    let isValid c =
        Array.contains
            c
            [| for c in Path.GetInvalidFileNameChars() do
                   yield c
               for c in Path.GetInvalidPathChars() do
                   yield c |]
    s
    |> Seq.map (fun c -> if isValid c then '_' else c)
    |> Seq.toArray
    |> System.String
