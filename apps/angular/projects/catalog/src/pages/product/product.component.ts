import { ListService } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { eCatalogPolicyNames } from '@catalog/config';
import { ProductDto, ProductService } from '@catalog/proxy/products';

@Component({
  selector: 'lib-product',
  templateUrl: './product.component.html',
  styleUrls: ['./product.component.css'],
  providers: [ListService],
})
export class ProductComponent implements OnInit {
  permissions = {
    create: eCatalogPolicyNames.ProductManagementCreate,
    update: eCatalogPolicyNames.ProductManagementUpdate,
    delete: eCatalogPolicyNames.ProductManagementDelete,
  };

  items: ProductDto[] = [];
  count = 0;

  selected: ProductDto;

  isModalVisible: boolean;

  modalBusy = false;

  form: FormGroup;
  constructor(
    public readonly productService: ProductService,
    public readonly list: ListService,
    private confirmationService: ConfirmationService,
    private fb: FormBuilder
  ) {
    // TODO: this is an example of paging
    this.list.maxResultCount = 2;
  }

  ngOnInit(): void {
    const productStreamCreator = query => this.productService.getListPaged(query);

    this.list.hookToQuery(productStreamCreator).subscribe(response => {
      this.items = response.items;
      this.count = response.totalCount;
    });
  }

  buildForm() {
    this.form = this.fb.group({
      name: [this.selected.name],
      code: [this.selected.code],
      price: [this.selected.price],
      stockCount: [this.selected.stockCount],
    });
  }

  onEdit(product: ProductDto) {
    this.selected = product;
    this.openModal();
  }

  onCreate() {
    this.selected = {} as ProductDto;
    this.openModal();
  }

  openModal() {
    this.isModalVisible = true;
    this.buildForm();
  }

  onDelete(product: ProductDto) {
    this.confirmationService
      .warn('AbpCatalog::ProductDeletionConfirmationMessage', 'AbpCatalog::AreYouSure', {
        messageLocalizationParams: [product.name],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          this.productService.delete(product.id).subscribe(() => this.list.get());
        }
      });
  }

  save() {}
}
