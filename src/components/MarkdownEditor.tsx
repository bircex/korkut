import { CKEditor } from "@ckeditor/ckeditor5-react";
import {
  Alignment,
  AutoImage,
  AutoLink,
  Autoformat,
  Base64UploadAdapter,
  BlockQuote,
  Bold,
  ClassicEditor,
  Code,
  CodeBlock,
  Essentials,
  FindAndReplace,
  Font,
  GeneralHtmlSupport,
  Heading,
  Highlight,
  HorizontalLine,
  HtmlEmbed,
  Image,
  ImageCaption,
  ImageInsert,
  ImageResize,
  ImageStyle,
  ImageToolbar,
  ImageUpload,
  Indent,
  IndentBlock,
  Italic,
  Link,
  LinkImage,
  List,
  ListProperties,
  Markdown,
  MediaEmbed,
  PageBreak,
  Paragraph,
  PasteFromOffice,
  PictureEditing,
  RemoveFormat,
  SelectAll,
  ShowBlocks,
  SourceEditing,
  SpecialCharacters,
  SpecialCharactersEssentials,
  Strikethrough,
  Subscript,
  Superscript,
  Table,
  TableCaption,
  TableCellProperties,
  TableColumnResize,
  TableProperties,
  TableToolbar,
  TextTransformation,
  TodoList,
  Underline,
  type Editor,
  type EditorConfig,
} from "ckeditor5";
import translations from "ckeditor5/translations/tr.js";
import "ckeditor5/ckeditor5.css";

// The single rich-text editor used across the app — the document Canvas and the Özelleştir theme
// editor — styled as a UYAP-like A4 page (see `.doc-canvas` in styles.css). Loaded with the full
// set of CKEditor open-source plugins. GeneralHtmlSupport lets rich formatting (colour, font,
// alignment) survive the Markdown round-trip as inline HTML. `data` is markdown; `getData()`
// returns markdown. (Premium features — track changes, comments, real pagination, Word/PDF
// export, AI — live in a separately-licensed package and are not included.)
const EDITOR_CONFIG: EditorConfig = {
  licenseKey: "GPL",
  language: "tr",
  translations: [translations],
  plugins: [
    Essentials, Autoformat, Paragraph, Heading,
    Bold, Italic, Underline, Strikethrough, Subscript, Superscript, Code, RemoveFormat,
    Font, Highlight, Alignment, Indent, IndentBlock,
    List, ListProperties, TodoList,
    Link, AutoLink, LinkImage, BlockQuote, CodeBlock,
    HorizontalLine, PageBreak,
    FindAndReplace, SelectAll, SpecialCharacters, SpecialCharactersEssentials,
    SourceEditing, GeneralHtmlSupport, HtmlEmbed, ShowBlocks,
    Image, ImageToolbar, ImageCaption, ImageStyle, ImageResize, ImageInsert, AutoImage,
    ImageUpload, PictureEditing, Base64UploadAdapter,
    MediaEmbed,
    Table, TableToolbar, TableProperties, TableCellProperties, TableColumnResize, TableCaption,
    PasteFromOffice, TextTransformation, Markdown,
  ],
  toolbar: {
    items: [
      "undo", "redo", "|",
      "sourceEditing", "showBlocks", "findAndReplace", "|",
      "heading", "|",
      "fontFamily", "fontSize", "fontColor", "fontBackgroundColor", "highlight", "|",
      "bold", "italic", "underline", "strikethrough", "subscript", "superscript", "code",
      "removeFormat", "|",
      "alignment", "|",
      "bulletedList", "numberedList", "todoList", "outdent", "indent", "|",
      "blockQuote", "codeBlock", "insertTable", "insertImage", "mediaEmbed", "horizontalLine",
      "pageBreak", "specialCharacters", "htmlEmbed", "link",
    ],
    shouldNotGroupWhenFull: false,
  },
  image: {
    toolbar: [
      "imageTextAlternative", "toggleImageCaption", "|",
      "imageStyle:inline", "imageStyle:block", "imageStyle:side", "|", "resizeImage",
    ],
  },
  table: {
    contentToolbar: [
      "tableColumn", "tableRow", "mergeTableCells", "tableProperties", "tableCellProperties",
    ],
  },
  htmlSupport: {
    allow: [{ name: /.*/, attributes: true, classes: true, styles: true }],
  },
  mediaEmbed: { previewsInData: true },
};

export function MarkdownEditor({
  value,
  onReady,
  onChange,
}: {
  value: string;
  onReady?: (editor: Editor) => void;
  onChange?: (markdown: string) => void;
}) {
  return (
    <CKEditor
      editor={ClassicEditor}
      config={EDITOR_CONFIG}
      data={value}
      onReady={(editor) => onReady?.(editor)}
      onChange={(_evt, editor) => onChange?.(editor.getData() as string)}
    />
  );
}
