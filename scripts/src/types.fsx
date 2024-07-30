type ArticleInfo =
    { Url: string
      Title: string
      Description: string
      Image: string
      Author: string
      Date: string
      Location: string
      FilePathPC: string
      FilePathMobile: string }
    static member Create(url) =
        { Url = url
          Title = null
          Description = null
          Image = null
          Author = null
          Date = null
          Location = null
          FilePathPC = null
          FilePathMobile = null }
    member this.DateTimeStr =
        let time = System.DateTime.ParseExact(this.Date, "yyyy年MM月dd日 HH:mm", null)
        time.ToString("yyyy-MM-dd_HH-mm")
