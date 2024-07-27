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
