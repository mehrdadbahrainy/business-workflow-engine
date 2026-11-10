# Local Lucide icons

Flowforge uses the [Lucide](https://lucide.dev/) icon collection, pinned to
[upstream commit e9cfd3c6d3b9447cefc019df97a99cc594bc0523](https://github.com/lucide-icons/lucide/commit/e9cfd3c6d3b9447cefc019df97a99cc594bc0523).

The [lucide-sprite.svg](lucide-sprite.svg) file is assembled from the official
`icons/*.svg` source files. Only the 16 icons currently needed by the interface
are vendored, without redrawing their shapes.

## Usage

The Angular template references icons through an external SVG sprite:

```html
<svg class="ui-icon" viewBox="0 0 24 24" fill="none"
     stroke="currentColor" stroke-width="2"
     stroke-linecap="round" stroke-linejoin="round"
     aria-hidden="true" focusable="false">
  <use href="/icons/lucide-sprite.svg#workflow"></use>
</svg>
```

Angular automatically copies the `public/icons` files into the built
application and the web server serves them from the **same origin**. Icons
inherit the existing `currentColor` CSS and hover styles.

No remote CDN, runtime npm package, JS icon loader or icon font is needed.

## Included symbols

- `workflow`
- `panels-top-left`
- `users-round`
- `plug`
- `shield-check`
- `chevron-right`
- `house`
- `clock-arrow-up`
- `list-todo`
- `log-out`
- `refresh-cw`
- `plus`
- `copy`
- `x`
- `play`
- `check`

For additions, take the official SVG from the pinned Lucide source,
add its paths/shapes to a new `<symbol id="...">` inside this sprite,
and reference the matching ID from the template. Keep any icon-only buttons
accessible with an appropriate button label or title.

## License

The upstream assets are provided under the **ISC License**;
the complete license text is retained in [LICENSE.txt](LICENSE.txt).
